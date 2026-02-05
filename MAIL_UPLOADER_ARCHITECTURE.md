# Mail Uploader Service - Architecture Design

## Overview

This document presents the architecture design for a Mail Uploader Service that connects to multiple email providers (Gmail/Outlook), processes incoming emails and attachments, and stores them in a SQL database with multi-tenant support.
Provider-specific ingestion reduces load: Outlook uses Microsoft Graph change notifications plus delta queries, while Gmail uses push notifications (Pub/Sub) where possible or polling with historyId/delta tokens when push is unavailable.

### Requirements

- Connect to Outlook/Gmail mailboxes (multiple accounts)
- Read incoming emails and attachments
- Store email metadata and content in SQL database
- Handle medium to high load (thousands of emails per hour)
- Support multiple tenants (each tenant has their own mailboxes)

---

## 1. High-Level Architecture Diagram

```mermaid
flowchart TB
    subgraph External["External Email Providers"]
        Gmail["Gmail API"]
        Outlook["Microsoft Graph API"]
    end

    subgraph Azure["Azure Cloud"]
        subgraph Gateway["API Layer"]
            APIM["Azure API Management"]
            AppGW["Azure Application Gateway"]
        end

        subgraph Compute["Compute Layer (C# / .NET 8)"]
            Functions["Azure Functions\n(Timer Triggers)"]
            Workers["Azure Container Apps\n(C# Worker Services)"]
            API["C# Web API\n(Container App)"]
        end

        subgraph Messaging["Messaging"]
            ServiceBus["Azure Service Bus\n(Queues & Topics)\nmessage: tenant_id + mailbox_id"]
        end

        subgraph Storage["Storage Layer"]
            SQL[(Azure SQL\nDatabase\nrows: tenant_id)]
            Blob[(Azure Blob\nStorage\ncontainer: tenant_id)]
            Redis[(Azure Cache\nfor Redis)]
        end

        subgraph Security["Security & Identity"]
            KeyVault["Azure Key Vault"]
            AAD["Azure AD B2C"]
        end

        subgraph Observability["Observability"]
            AppInsights["Application Insights"]
            Monitor["Azure Monitor"]
        end
    end

    Gmail --> Workers
    Outlook --> Workers
    
    AppGW --> APIM
    APIM --> API
    API --> SQL
    
    Functions --> ServiceBus
    Workers --> ServiceBus
    ServiceBus --> Workers
    
    Workers --> SQL
    Workers --> Blob
    
    Workers --> KeyVault
    Workers --> Redis
    API --> Redis
    
    Workers --> AppInsights
    API --> AppInsights
    Functions --> AppInsights

    TenantIsolationNote["Tenant isolation enforced via tenant_id + per-tenant rate limiting + row-level filters"]
    TenantIsolationNote -.-> ServiceBus
    TenantIsolationNote -.-> SQL
```

Tenant isolation: tenant_id on every row + per-tenant rate limiting + queue sessions by tenant_id.

### Architecture Components

| Component | Technology | Purpose |
|-----------|------------|---------|
| **Load Balancer** | Azure Application Gateway | Distribute incoming API requests |
| **API Gateway** | Azure API Management | Authentication, rate limiting, routing |
| **Scheduler Service** | .NET 8 + Azure Functions (Timer Trigger) | Orchestrate email sync jobs |
| **Mail Connector** | .NET 8 Worker Service | Connect to email providers via OAuth2 |
| **Email Processor** | .NET 8 Worker Service | Parse and process email content |
| **Message Broker** | Azure Service Bus | Decouple ingestion from processing |
| **Database** | Azure SQL Database | Store email metadata and relationships |
| **Object Storage** | Azure Blob Storage | Store email attachments |
| **Cache** | Azure Cache for Redis | Cache tokens, sync states, rate limits |
| **Secrets Manager** | Azure Key Vault | Securely store OAuth credentials |

---

## 2. Component Diagram

```mermaid
flowchart TB
    subgraph Tenant_Management["Tenant Management (C#)"]
        TenantAPI["Tenant API\n• CRUD tenants\n• Manage mailboxes\n• Configure settings\n• REST endpoints"]
        AuthService["Azure AD B2C\n• JWT validation\n• MSAL integration\n• Rate limiting via APIM"]
    end

    subgraph Email_Ingestion["Email Ingestion Layer (C# / .NET 8)"]
        Scheduler["Azure Functions\n• Timer triggers\n• Distribute work\n• Handle backpressure"]

        WebhookReceiver["Webhook Receiver (C# Minimal API)\n• Receive Graph change notifications\n• Receive Gmail push (Pub/Sub -> HTTPS)\n• Validate provider signature/token\n• Map notification to tenant/mailbox\n• Enqueue mailbox sync job"]
        
        GmailConnector["Gmail Connector\n• Google.Apis.Gmail\n• OAuth2 via MSAL\n• Delta sync tokens"]
        
        OutlookConnector["Outlook Connector\n• Microsoft.Graph SDK\n• OAuth2 via MSAL\n• Change notifications"]
    end

    subgraph Processing["Processing Layer (C# Worker Services)"]
        EmailQueue["Azure Service Bus\n• Priority queues\n• Dead letter queue\n• Sessions for ordering"]
        
        EmailProcessor["Email Processor\n• MimeKit parsing\n• Extract metadata\n• Polly retry policies"]
        
        AttachmentProcessor["Attachment Processor\n• Block blob upload\n• Azure Defender scan\n• Compression"]
    end

    subgraph Persistence["Persistence Layer"]
        DBService["Entity Framework Core\n• Azure SQL\n• Connection pooling\n• Migrations"]
        
        StorageService["Azure Blob SDK\n• Block blob upload\n• SAS token URLs\n• Lifecycle policies"]
        
        CacheService["StackExchange.Redis\n• Token caching\n• Distributed cache\n• Rate limit counters"]
    end

    subgraph Observability["Azure Observability"]
        AppInsights["Application Insights\n(Metrics + Traces)"]
        LogAnalytics["Log Analytics\n(Centralized Logs)"]
        AzureMonitor["Azure Monitor\n(Alerts + Dashboards)"]
    end

    TenantAPI --> Scheduler
    AuthService --> TenantAPI

    Gmail --> WebhookReceiver
    Outlook --> WebhookReceiver
    
    Scheduler --> GmailConnector
    Scheduler --> OutlookConnector

    WebhookReceiver --> EmailQueue
    
    GmailConnector --> EmailQueue
    OutlookConnector --> EmailQueue
    
    EmailQueue --> EmailProcessor
    EmailProcessor --> AttachmentProcessor
    
    EmailProcessor --> DBService
    AttachmentProcessor --> StorageService
    
    GmailConnector --> CacheService
    OutlookConnector --> CacheService
    
    EmailProcessor --> AppInsights
    EmailProcessor --> LogAnalytics
    AzureMonitor --> AppInsights
```

### Service Responsibilities

#### Tenant Management Layer
- **Tenant API** (C# REST API): Manages tenant lifecycle, mailbox registration, and configuration
- **Auth Service** (Azure AD B2C + MSAL): Handles API authentication, JWT validation, and rate limiting per tenant

#### Email Ingestion Layer
- **Scheduler Service** (C# Azure Functions): Triggers sync jobs based on configured intervals
- **Gmail Connector** (C# Worker + Google.Apis.Gmail): Implements Gmail API with OAuth2 and delta sync
- **Outlook Connector** (C# Worker + Microsoft.Graph): Implements Graph API with OAuth2 and change notifications

#### Processing Layer
- **Email Queue** (Azure Service Bus Queues): Manages job distribution with sessions and retry policies
- **Email Processor** (C# Worker + MimeKit): Parses MIME content, extracts headers, body, and metadata
- **Attachment Processor** (C# Worker): Handles large file streaming with Azure Blob SDK

#### Persistence Layer
- **Database Service** (C# + Entity Framework Core): Manages Azure SQL connections with pooling and transactions
- **Storage Service** (C# + Azure.Storage.Blobs): Handles blob storage operations with block uploads
- **Cache Service** (C# + StackExchange.Redis): Provides fast access to frequently used data (tokens, sync state)

---

## 3. Data Flow Diagram

```mermaid
sequenceDiagram
    autonumber
    participant Scheduler as Azure Function<br/>(Timer Trigger)
    participant Connector as C# Worker<br/>(Mail Connector)
    participant Provider as Email Provider<br/>(Gmail/Outlook)
    participant Queue as Azure Service Bus
    participant Processor as C# Worker<br/>(Email Processor)
    participant DB as Azure SQL
    participant Storage as Azure Blob Storage
    participant Cache as Azure Redis Cache
    participant KeyVault as Azure Key Vault

    Note over Scheduler: Triggered by Timer (every 5 min)
    
    Scheduler->>DB: Get mailboxes due for sync
    DB-->>Scheduler: List of mailboxes
    
    loop For each mailbox
        Scheduler->>Queue: Send message to sync-jobs queue
    end

    Queue->>Connector: Receive sync job message
    Connector->>Cache: Get OAuth token
    
    alt Token expired
        Connector->>KeyVault: Get refresh token
        Connector->>Provider: Refresh access token
        Provider-->>Connector: New access token
        Connector->>Cache: Store new token (TTL: 50 min)
    end

    Connector->>Provider: Fetch emails since last sync (delta query)
    Provider-->>Connector: Email list with metadata

    loop For each email
        Connector->>Queue: Send to email-processing queue
    end

    Queue->>Processor: Receive email processing message (PeekLock)
    Processor->>Provider: Fetch full email content
    Provider-->>Processor: Email with attachments

    Processor->>Processor: Parse MIME with MimeKit

    alt Has attachments
        loop For each attachment
            Processor->>Storage: Upload block blob
            Storage-->>Processor: Blob URI reference
        end
    end

    Processor->>DB: Store email metadata (EF Core)
    Processor->>DB: Store attachment references
    Processor->>DB: Update sync state
    Processor->>Queue: Complete message only after DB transaction commit
    alt Transient failure
        Processor->>Queue: Abandon/Defer (retry with exponential backoff)
        Queue-->>Queue: Move to DLQ after max retries (error metadata)
    end
```

### Data Flow Steps

1. **Trigger**: Scheduler runs on cron (e.g., every 5 minutes) or receives webhook notification
2. **Job Distribution**: Mailboxes needing sync are queued as individual jobs
3. **Authentication**: Connector retrieves/refreshes OAuth tokens from cache
4. **Delta Sync**: Connector fetches only new emails using provider's sync tokens
5. **Processing Queue**: Each email is enqueued for parallel processing
6. **Content Parsing**: Processor fetches full email and parses MIME structure
7. **Attachment Upload**: Large attachments are streamed directly to object storage
8. **Persistence**: Email metadata and attachment references are stored in database
9. **State Update**: Sync token is updated to enable efficient delta sync next time

---

## 3.1 Idempotency & Deduplication
- **Email unique key**: `(mailbox_id, provider_message_id)` with a unique constraint; mailbox_id implies tenant.
- **Attachment dedup**: content hash (e.g., MD5/SHA256) + size to avoid duplicate storage.
- **Retry safety**: processing is idempotent; the broker provides at-least-once delivery and duplicates are filtered by the unique key and attachment hash.

## 4. Database Schema Design

```mermaid
erDiagram
    tenants ||--o{ mailboxes : "has"
    tenants ||--o{ api_keys : "has"
    mailboxes ||--o{ emails : "contains"
    mailboxes ||--|| sync_states : "tracks"
    emails ||--o{ attachments : "has"
    emails ||--o{ email_recipients : "has"

    tenants {
        uniqueidentifier id PK "NEWSEQUENTIALID()"
        nvarchar(255) name
        nvarchar(50) plan_tier
        nvarchar(max) settings "JSON column"
        bit is_active
        datetime2 created_at
        datetime2 updated_at
        rowversion version "Optimistic concurrency"
    }

    api_keys {
        uniqueidentifier id PK
        uniqueidentifier tenant_id FK
        nvarchar(256) key_hash
        nvarchar(100) name
        datetime2 expires_at
        datetime2 created_at
    }

    mailboxes {
        uniqueidentifier id PK
        uniqueidentifier tenant_id FK
        nvarchar(20) provider "gmail|outlook"
        nvarchar(320) email_address UK
        nvarchar(500) oauth_token_ref "Key Vault reference"
        bit is_active
        datetime2 created_at
        datetime2 updated_at
        rowversion version
    }

    sync_states {
        uniqueidentifier id PK
        uniqueidentifier mailbox_id FK
        nvarchar(500) sync_token "Provider delta token"
        nvarchar(200) last_message_id
        datetime2 last_sync_at
        nvarchar(20) status "syncing|idle|error"
        int error_count
        nvarchar(max) last_error
        rowversion version
    }

    emails {
        uniqueidentifier id PK
        uniqueidentifier mailbox_id FK
        nvarchar(200) provider_message_id UK
        nvarchar(200) internet_message_id
        nvarchar(200) thread_id
        nvarchar(200) conversation_id
        nvarchar(1000) subject
        nvarchar(320) from_address
        nvarchar(max) body_text
        nvarchar(max) body_html
        datetime2 received_at "Partition key"
        datetime2 processed_at
        nvarchar(20) processing_status "queued|processing|processed|failed"
        datetime2 processing_started_at
        datetime2 processing_completed_at
        bit has_attachments
        nvarchar(max) headers "JSON column"
        datetime2 created_at
    }

    email_recipients {
        uniqueidentifier id PK
        uniqueidentifier email_id FK
        nvarchar(10) recipient_type "to|cc|bcc"
        nvarchar(320) email_address
        nvarchar(255) display_name
    }

    attachments {
        uniqueidentifier id PK
        uniqueidentifier email_id FK
        nvarchar(500) filename
        nvarchar(100) content_type
        bigint size_bytes
        nvarchar(100) storage_container
        nvarchar(500) blob_name
        nvarchar(32) checksum_md5
        datetime2 created_at
    }
```

### Table Descriptions

| Table | Description |
|-------|-------------|
| **tenants** | Organizations using the service, with plan tier and settings |
| **api_keys** | API authentication keys per tenant for secure access |
| **mailboxes** | Email accounts registered for syncing, linked to tenants |
| **sync_states** | Tracks sync progress per mailbox with delta tokens |
| **emails** | Email metadata including subject, body, headers, provider/internet IDs, thread/conversation IDs, and processing status |
| **email_recipients** | Normalized recipients (to, cc, bcc) per email |
| **attachments** | Attachment metadata with references to object storage |

Relationships and constraints: tenants → mailboxes → emails → attachments/recipients, with foreign keys and a unique constraint on `(mailbox_id, provider_message_id)` to prevent duplicates.

### Index Strategy (Azure SQL)

```sql
-- Performance indexes for common queries
CREATE NONCLUSTERED INDEX IX_mailboxes_tenant_active 
    ON mailboxes(tenant_id, is_active) INCLUDE (email_address, provider);

CREATE NONCLUSTERED INDEX IX_emails_mailbox_received 
    ON emails(mailbox_id, received_at DESC) INCLUDE (subject, from_address);

CREATE UNIQUE NONCLUSTERED INDEX IX_emails_provider_message 
    ON emails(mailbox_id, provider_message_id);

CREATE NONCLUSTERED INDEX IX_emails_internet_message
    ON emails(mailbox_id, internet_message_id);

CREATE NONCLUSTERED INDEX IX_emails_thread_conversation
    ON emails(mailbox_id, thread_id, conversation_id);

CREATE NONCLUSTERED INDEX IX_emails_processing_status
    ON emails(mailbox_id, processing_status, processing_started_at);

CREATE NONCLUSTERED INDEX IX_sync_states_status 
    ON sync_states(status, last_sync_at);

CREATE NONCLUSTERED INDEX IX_attachments_email 
    ON attachments(email_id) INCLUDE (filename, content_type, size_bytes);

-- Full-text search on email content (Azure SQL Full-Text Search)
CREATE FULLTEXT CATALOG EmailsCatalog AS DEFAULT;
CREATE FULLTEXT INDEX ON emails(subject, body_text) 
    KEY INDEX PK_emails ON EmailsCatalog;
```

### Partitioning Strategy (Azure SQL)

```sql
-- Create partition function for monthly partitions
CREATE PARTITION FUNCTION PF_EmailsByMonth (DATETIME2)
AS RANGE RIGHT FOR VALUES (
    '2024-01-01', '2024-02-01', '2024-03-01', '2024-04-01',
    '2024-05-01', '2024-06-01', '2024-07-01', '2024-08-01',
    '2024-09-01', '2024-10-01', '2024-11-01', '2024-12-01'
);

-- Create partition scheme
CREATE PARTITION SCHEME PS_EmailsByMonth
AS PARTITION PF_EmailsByMonth ALL TO ([PRIMARY]);

-- Create partitioned emails table
CREATE TABLE emails (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    mailbox_id UNIQUEIDENTIFIER NOT NULL,
    received_at DATETIME2 NOT NULL,
    -- ... other columns
) ON PS_EmailsByMonth(received_at);
```

---

## 5. Scaling Strategy and Failure Handling

### Scaling Architecture (Azure)

```mermaid
flowchart LR
    subgraph Horizontal_Scaling["Azure Container Apps / AKS"]
        direction TB
        A["C# Mail Connectors\n(KEDA auto-scale 2-20)"]
        B["C# Email Processors\n(KEDA auto-scale 5-50)"]
        C["C# API Containers\n(HTTP auto-scale 2-10)"]
    end

    subgraph Queue_Partitioning["Azure Service Bus"]
        direction TB
        D["Sessions by Tenant ID\n(ordered processing)"]
        E["Priority Queues:\n• High: Webhooks\n• Normal: Polling\n• Low: Retry/DLQ"]
    end

    subgraph Database_Scaling["Azure SQL"]
        direction TB
        F["Read Replicas\n(geo-replication)"]
        G["Table Partitioning\n(by received_at)"]
        H["Elastic Pools\n(shared DTUs)"]
    end

    subgraph Caching_Strategy["Azure Cache for Redis"]
        direction TB
        I["OAuth Tokens\n(TTL: 50 min)"]
        J["Sync States\n(TTL: 5 min)"]
        K["Rate Limit Counters\n(sliding window)"]
    end
```

### Scaling Approach (Azure)

| Component | Scaling Method | Trigger Metrics |
|-----------|---------------|-----------------|
| **Mail Connectors** (C#) | Azure Container Apps / AKS autoscaling | Service Bus queue depth > 1000 |
| **Email Processors** (C#) | KEDA (Kubernetes Event-Driven Autoscaling) | Service Bus message count > threshold |
| **API Gateway** | Azure API Management auto-scale | Request rate > 1000 RPS |
| **Database** | Azure SQL Elastic Pools + Read Replicas | DTU usage > 80% |
| **Object Storage** | Azure Blob Storage (inherently scalable) | N/A |
| **Message Queue** | Azure Service Bus (Premium recommended for high throughput) | Built-in scaling |
| **Azure Functions** | Consumption plan (automatic scaling) | Event-driven |

### Throughput Calculations

For **thousands of emails per hour**:

```
Target: 5,000 emails/hour = ~1.4 emails/second

With 10 processor workers:
- Each worker handles: 0.14 emails/second
- Processing time budget: ~7 seconds per email
- Includes: API fetch, parse, attachment upload, DB write

Buffer for burst (3x peak):
- 15,000 emails/hour = ~4.2 emails/second
- 20 workers = 0.21 emails/second each
- Processing time budget: ~4.7 seconds per email
```

### Failure Handling Strategy

```mermaid
flowchart TB
    subgraph Retry_Strategy["Retry Strategy (Polly)"]
        R1["Exponential Backoff\n1s → 2s → 4s → 8s → 16s"]
        R2["Max Retries: 5"]
        R3["Service Bus Dead Letter\nafter exhaustion"]
    end

    subgraph Circuit_Breaker["Circuit Breaker (Polly)"]
        CB1["Monitor failure rate\nper email provider"]
        CB2["Open circuit at 50%\nfailure rate (5 min window)"]
        CB3["Half-open: test with\n1 request per 30s"]
        CB4["Close after 3 consecutive\nsuccesses"]
    end

    subgraph Failure_Scenarios["Failure Scenarios & Handling"]
        F1["OAuth Token Expired\n→ MSAL auto-refresh"]
        F2["Provider Rate Limited\n→ Polly RateLimitPolicy"]
        F3["Network Timeout\n→ HttpClient retry policy"]
        F4["Azure SQL Unavailable\n→ EF Core retry policy"]
        F5["Blob Upload Failed\n→ Block blob retry"]
    end

    subgraph Monitoring["Azure Monitor Alerting"]
        M1["Alert: DLQ depth > 100"]
        M2["Alert: Sync lag > 1 hour"]
        M3["Alert: Error rate > 5%"]
        M4["Auto-recovery via\nAzure Automation"]
    end
```

### Failure Handling Details

#### 0. Backpressure and Autoscaling
- Use queue depth to throttle ingestion and scale workers (KEDA/ACA) to avoid overloading providers and the database.
- When depth exceeds thresholds, slow scheduling or pause new mailbox sync jobs.

#### 0.1 Retry Policy + DLQ Rules
- Exponential backoff with bounded retries for transient failures.
- After retry exhaustion, move messages to DLQ with error metadata for investigation and replay.

#### 1. Idempotency
- Use `(mailbox_id, provider_message_id)` as the deduplication key with a unique constraint (mailbox is tenant-scoped)
- EF Core upsert operations prevent duplicate email storage
- Attachment uploads use MD5 content hash for verification

#### 2. At-Least-Once Delivery (Azure Service Bus)
- Use `PeekLock` mode - messages acknowledged only after successful processing
- EF Core `SaveChangesAsync()` commits before `CompleteMessageAsync()`
- Service Bus duplicate detection window (10 min) as safety net

#### 3. Graceful Degradation
- If Blob Storage fails, store email metadata without attachments
- Flag email for attachment retry in `sync_states.last_error`
- Continue processing remaining emails in the batch

#### 3.1 Partial Attachment Failures
- If some attachments fail, mark email `processing_status=failed` and retry only failed attachments without re-ingesting the email.

#### 4. Multi-Tenant Isolation
- Service Bus Sessions grouped by `tenant_id` for ordered processing
- Per-tenant rate limits via Azure API Management policies
- Tenant-specific Polly circuit breakers isolate provider failures

#### 4.1 Token/Auth Failures and Mailbox Health
- On token refresh failures, mark mailbox health as `error` and pause further sync until re-authenticated.
- Expose mailbox health status for operational visibility and alerting.

#### 5. Data Consistency
- EF Core transactions with `IsolationLevel.ReadCommitted`
- Optimistic concurrency with `[Timestamp]` attribute on sync state
- Soft deletes with `DeletedAt` column for compliance requirements

#### 6. C# Resilience Patterns (Polly)
```csharp
// Example Polly policy configuration in C#
services.AddHttpClient<IGmailClient>()
    .AddPolicyHandler(Policy
        .WaitAndRetryAsync(5, retryAttempt => 
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
    .AddPolicyHandler(Policy
        .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1)));
```

### Monitoring & Alerting (Azure Monitor)

| Metric | Warning Threshold | Critical Threshold | Azure Service |
|--------|-------------------|-------------------|---------------|
| Queue Depth | > 5,000 messages | > 20,000 messages | Service Bus Metrics |
| Processing Latency (p99) | > 10 seconds | > 30 seconds | Application Insights |
| Error Rate | > 1% | > 5% | Application Insights |
| Sync Lag | > 30 minutes | > 2 hours | Custom Metrics |
| Dead Letter Queue Size | > 50 messages | > 200 messages | Service Bus DLQ |
| OAuth Refresh Failures | > 3 per mailbox | > 10 per tenant | Application Insights |
| DTU Usage | > 70% | > 90% | Azure SQL Metrics |
| Blob Storage Latency | > 500ms | > 2000ms | Storage Analytics |

**Azure Alerting Setup:**
- Use Azure Monitor Action Groups for notifications (Email, SMS, Teams, PagerDuty)
- Configure Log Analytics workspace for centralized logging
- Set up Application Insights dashboards for real-time monitoring
- Use Azure Workbooks for custom reporting and visualization

---

## Summary

This architecture provides:

| Aspect | Implementation |
|--------|---------------|
| **Scalability** | Azure Container Apps with KEDA, Service Bus partitioning, Azure SQL elastic pools |
| **Reliability** | Polly retry/circuit breaker, Service Bus dead letter queues, Azure SQL geo-replication |
| **Multi-tenancy** | Service Bus sessions, APIM rate limiting, Key Vault credential isolation |
| **Performance** | Async/await patterns, Azure Cache for Redis, EF Core connection pooling |
| **Observability** | Application Insights distributed tracing, Log Analytics, Azure Monitor dashboards |

The design handles **thousands of emails per hour** by:
1. Decoupling ingestion from processing through Azure Service Bus queues
2. KEDA-based autoscaling of C# Worker containers based on queue depth
3. Using delta sync tokens to minimize API calls to email providers
4. Streaming large attachments directly to Azure Blob Storage (block blobs)
5. Caching OAuth tokens and sync states in Azure Cache for Redis

---

## Why This Architecture Was Designed This Way
- **Broker-decoupled ingestion** separates mailbox polling/webhooks from processing to handle bursty provider traffic and meet throughput requirements without dropping messages.
- **Provider-specific delta sync** (Gmail historyId, Graph delta + change notifications) reduces API load and rate-limit risk compared to full mailbox polling, aligning with scalability requirements.
- **Multi-tenancy enforced at API, queue, and DB layers** ensures data isolation and predictable per-tenant behavior under load, matching the assignment’s multi-tenant requirement.
- **Blob storage for attachments** keeps large binaries out of SQL to reduce database load and improve upload reliability, supporting high-volume ingestion.
- **Idempotency and deduplication** prevent duplicate storage under at-least-once delivery and retries, preserving correctness.
- **Queue-driven autoscaling** reacts to actual backlog rather than schedules, improving throughput and cost-efficiency for variable email volume.
- **Eventual consistency** is acceptable because ingestion pipelines are asynchronous by design; it maximizes reliability and keeps the system responsive under load.

---

## Technology Stack (C# / .NET 8 + Azure)

| Layer | Technology | Purpose |
|-------|------------|---------|
| **Language** | C# 12 | Primary programming language |
| **Runtime** | .NET 8 | Modern, high-performance runtime |
| **Web API** | C# REST API (Minimal APIs) | RESTful API endpoints |
| **Background Workers** | C# Worker Service | Long-running email processing |
| **Serverless** | C# Azure Functions | Timer triggers, event processing |
| **Message Queue** | Azure Service Bus | Enterprise messaging with queues/topics |
| **Database** | Azure SQL Database | Managed SQL with auto-scaling |
| **Object Storage** | Azure Blob Storage | Attachment storage with tiers |
| **Cache** | Azure Cache for Redis | Distributed caching |
| **Secrets** | Azure Key Vault | Secure credential management |
| **Identity** | Azure AD / MSAL | OAuth2 token management |
| **Container Orchestration** | Azure Container Apps / AKS | Container management |
| **Observability** | Azure Monitor + Application Insights | Logging, metrics, tracing |
| **API Management** | Azure API Management | Gateway, throttling, policies |

### Key C# / .NET Libraries

| Library | Purpose |
|---------|---------|
| **Microsoft.Graph** | Microsoft 365 / Outlook API client |
| **Google.Apis.Gmail.v1** | Gmail API client |
| **MimeKit** | Email MIME parsing |
| **Azure.Messaging.ServiceBus** | Service Bus SDK |
| **Azure.Storage.Blobs** | Blob Storage SDK |
| **Microsoft.EntityFrameworkCore** | ORM for Azure SQL |
| **Polly** | Resilience and retry policies |
| **MediatR** | CQRS and mediator pattern |

---

## Azure Deployment Architecture

```mermaid
flowchart TB
    subgraph Internet["Internet"]
        Users["API Consumers"]
        Gmail["Gmail API"]
        Outlook["Microsoft Graph API"]
    end

    subgraph Azure["Azure Subscription"]
        subgraph RG_Network["Resource Group: Network"]
            AppGW["Application Gateway\n(WAF v2)"]
            VNet["Virtual Network"]
        end

        subgraph RG_Compute["Resource Group: Compute"]
            APIM["API Management\n(Developer/Standard)"]
            
            subgraph ACA["Azure Container Apps Environment"]
                API_App["mail-api\n(C# Web API)"]
                Connector_App["mail-connector\n(C# Worker)"]
                Processor_App["mail-processor\n(C# Worker)"]
            end
            
            FuncApp["Azure Functions\n(Consumption Plan)"]
        end

        subgraph RG_Data["Resource Group: Data"]
            SQL["Azure SQL Database\n(General Purpose)"]
            Blob["Storage Account\n(Blob Containers)"]
            Redis["Azure Cache for Redis\n(Standard C1)"]
            ServiceBus["Service Bus Namespace\n(Standard Tier; Premium recommended for high throughput)"]
        end

        subgraph RG_Security["Resource Group: Security"]
            KeyVault["Azure Key Vault"]
            AAD["Azure AD B2C"]
            ManagedId["Managed Identities"]
        end

        subgraph RG_Monitoring["Resource Group: Monitoring"]
            AppInsights["Application Insights"]
            LogAnalytics["Log Analytics\nWorkspace"]
            ActionGroups["Action Groups\n(Alerts)"]
        end
    end

    Users --> AppGW
    AppGW --> APIM
    APIM --> API_App
    
    FuncApp --> ServiceBus
    ServiceBus --> Connector_App
    ServiceBus --> Processor_App
    
    Connector_App --> Gmail
    Connector_App --> Outlook
    
    API_App --> SQL
    Processor_App --> SQL
    Processor_App --> Blob
    
    Connector_App --> Redis
    Processor_App --> Redis
    
    API_App --> KeyVault
    Connector_App --> KeyVault
    
    API_App --> AppInsights
    Connector_App --> AppInsights
    Processor_App --> AppInsights
    FuncApp --> AppInsights
    
    ManagedId --> KeyVault
    ManagedId --> SQL
    ManagedId --> Blob
```