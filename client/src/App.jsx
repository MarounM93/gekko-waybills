import { useEffect, useMemo, useState } from 'react'
import { apiGet, apiPostForm, apiPostJson, apiPutJson } from './api'
import './App.css'

function App() {
  const [tenantId, setTenantId] = useState(() => localStorage.getItem('tenantId') || 'TENANT001')
  const [importFile, setImportFile] = useState(null)
  const [importAsync, setImportAsync] = useState(false)
  const [importResult, setImportResult] = useState(null)
  const [importLoading, setImportLoading] = useState(false)
  const [importError, setImportError] = useState('')
  const [importJob, setImportJob] = useState(null)
  const [importJobLoading, setImportJobLoading] = useState(false)
  const [importJobError, setImportJobError] = useState('')

  const [statusFilter, setStatusFilter] = useState('')
  const [searchFilter, setSearchFilter] = useState('')
  const [waybills, setWaybills] = useState([])
  const [waybillsTotal, setWaybillsTotal] = useState(0)
  const [waybillsLoading, setWaybillsLoading] = useState(false)
  const [waybillsError, setWaybillsError] = useState('')
  const [editTarget, setEditTarget] = useState(null)
  const [editForm, setEditForm] = useState(null)
  const [editSaving, setEditSaving] = useState(false)
  const [editMessage, setEditMessage] = useState('')
  const [editError, setEditError] = useState('')

  useEffect(() => {
    localStorage.setItem('tenantId', tenantId)
  }, [tenantId])

  const statusOptions = useMemo(() => ([
    '',
    'PENDING',
    'DELIVERED',
    'CANCELLED',
    'DISPUTED'
  ]), [])

  const handleImport = async () => {
    if (!importFile) {
      setImportError('Please select a CSV file.')
      return
    }

    setImportLoading(true)
    setImportError('')
    setImportResult(null)
    setImportJob(null)
    setImportJobError('')
    try {
      const formData = new FormData()
      formData.append('file', importFile)
      if (importAsync) {
        const result = await apiPostForm('/api/waybills/import?async=true', tenantId, formData)
        setImportJob({ jobId: result.jobId, status: 'QUEUED' })
      } else {
        const result = await apiPostForm('/api/waybills/import', tenantId, formData)
        setImportResult(result)
      }
    } catch (error) {
      setImportError(error.message || 'Import failed.')
    } finally {
      setImportLoading(false)
    }
  }

  const loadWaybills = async (options = {}) => {
    setWaybillsLoading(true)
    setWaybillsError('')
    try {
      const params = new URLSearchParams({ page: '1', pageSize: '50' })
      if (statusFilter) params.set('status', statusFilter)
      if (searchFilter) params.set('search', searchFilter)
      if (options.refresh) params.set('refresh', 'true')
      const result = await apiGet(`/api/waybills?${params.toString()}`, tenantId)
      setWaybills(result.items || [])
      setWaybillsTotal(result.totalCount || 0)
    } catch (error) {
      setWaybillsError(error.message || 'Failed to load waybills.')
    } finally {
      setWaybillsLoading(false)
    }
  }

  useEffect(() => {
    if (!importJob?.jobId) return
    let isActive = true
    let timer

    const poll = async () => {
      setImportJobLoading(true)
      try {
        const result = await apiGet(`/api/import-jobs/${importJob.jobId}`, tenantId)
        if (!isActive) return
        setImportJob(result)
        if (result.status === 'SUCCEEDED' || result.status === 'FAILED') {
          setImportJobLoading(false)
          return
        }
        timer = setTimeout(poll, 2000)
      } catch (error) {
        if (!isActive) return
        setImportJobError(error.message || 'Failed to load job status.')
        timer = setTimeout(poll, 2000)
      }
    }

    poll()
    return () => {
      isActive = false
      if (timer) clearTimeout(timer)
    }
  }, [importJob?.jobId, tenantId])

  const openEdit = (item) => {
    setEditMessage('')
    setEditError('')
    setEditTarget(item)
    setEditForm({
      status: item.status,
      deliveryDate: item.deliveryDate,
      quantity: item.quantity,
      unitPrice: item.unitPrice,
      totalAmount: item.totalAmount,
      productCode: item.productCode || ''
    })
  }

  const closeEdit = () => {
    setEditTarget(null)
    setEditForm(null)
    setEditMessage('')
    setEditError('')
  }

  const handleEditChange = (field, value) => {
    setEditForm((prev) => ({ ...prev, [field]: value }))
  }

  const saveEdit = async () => {
    if (!editTarget || !editForm) return
    setEditSaving(true)
    setEditMessage('')
    setEditError('')
    try {
      const payload = {
        rowVersionBase64: editTarget.rowVersionBase64,
        deliveryDate: editForm.deliveryDate,
        productCode: editForm.productCode || '',
        quantity: Number(editForm.quantity),
        unitPrice: Number(editForm.unitPrice),
        totalAmount: Number(editForm.totalAmount),
        status: editForm.status
      }
      await apiPutJson(`/api/waybills/${editTarget.id}`, tenantId, payload)
      setEditMessage('Waybill updated successfully.')
      await loadWaybills()
      closeEdit()
    } catch (error) {
      if (error.status === 409) {
        setEditError('Waybill was modified by another user. Please reload.')
        await loadWaybills()
      } else if (error.status === 400) {
        setEditError(error.data?.error || error.message || 'Validation failed.')
      } else {
        setEditError(error.message || 'Update failed.')
      }
    } finally {
      setEditSaving(false)
    }
  }

  return (
    <div className="page">
      <header className="page-header">
        <h1>Waybills Console</h1>
        <p className="subtitle">Simple multi-tenant UI for imports and listing</p>
      </header>

      <section className="card">
        <h2>Tenant</h2>
        <div className="field-row">
          <label htmlFor="tenantId">Tenant ID</label>
          <input
            id="tenantId"
            type="text"
            value={tenantId}
            onChange={(event) => setTenantId(event.target.value)}
          />
        </div>
        <div className="pill">Current tenant: {tenantId}</div>
      </section>

      <section className="card">
        <h2>CSV Import</h2>
        <div className="field-row">
          <label htmlFor="importFile">CSV file</label>
          <input
            id="importFile"
            type="file"
            accept=".csv"
            onChange={(event) => setImportFile(event.target.files?.[0] || null)}
          />
          <label className="checkbox">
            <input
              type="checkbox"
              checked={importAsync}
              onChange={(event) => setImportAsync(event.target.checked)}
            />
            Import asynchronously
          </label>
          <button onClick={handleImport} disabled={importLoading}>
            {importLoading ? 'Importing...' : 'Import CSV'}
          </button>
        </div>
        {importError && <div className="error">{importError}</div>}
        {importJob && (
          <div className="job-panel">
            <div className="job-header">
              <strong>Job ID:</strong> {importJob.jobId || importJob.id}
            </div>
            <div className="job-grid">
              <div>
                <span>Status</span>
                <strong>{importJob.status}</strong>
              </div>
              {importJob.progressPercent !== null && importJob.progressPercent !== undefined && (
                <div>
                  <span>Progress</span>
                  <strong>{importJob.progressPercent}%</strong>
                </div>
              )}
              <div>
                <span>Total rows</span>
                <strong>{importJob.totalRows ?? '-'}</strong>
              </div>
              <div>
                <span>Inserted</span>
                <strong>{importJob.insertedCount ?? '-'}</strong>
              </div>
              <div>
                <span>Updated</span>
                <strong>{importJob.updatedCount ?? '-'}</strong>
              </div>
              <div>
                <span>Rejected</span>
                <strong>{importJob.rejectedCount ?? '-'}</strong>
              </div>
            </div>
            {importJobLoading && <div className="pill">Job running...</div>}
            {importJob.status === 'FAILED' && (
              <div className="error">
                {importJob.error || importJobError || 'Import failed.'}
              </div>
            )}
            {importJob.status === 'SUCCEEDED' && (
              <div className="success">Import completed successfully.</div>
            )}
            {importJobError && importJob.status !== 'FAILED' && (
              <div className="error">{importJobError}</div>
            )}
          </div>
        )}
        {importResult && (
          <div className="import-result">
            <div className="summary-grid">
              <div>
                <span>Total rows</span>
                <strong>{importResult.totalRows ?? 0}</strong>
              </div>
              <div>
                <span>Inserted</span>
                <strong>{importResult.insertedCount ?? 0}</strong>
              </div>
              <div>
                <span>Updated</span>
                <strong>{importResult.updatedCount ?? 0}</strong>
              </div>
              <div>
                <span>Rejected</span>
                <strong>{importResult.rejectedCount ?? 0}</strong>
              </div>
            </div>

            {importResult.rejectedRows?.length > 0 && (
              <>
                <h3>Rejected Rows</h3>
                <table>
                  <thead>
                    <tr>
                      <th>Row</th>
                      <th>Errors</th>
                    </tr>
                  </thead>
                  <tbody>
                    {importResult.rejectedRows.map((row) => (
                      <tr key={`rej-${row.rowNumber}`}>
                        <td>{row.rowNumber}</td>
                        <td>{(row.errors || []).join(', ')}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </>
            )}

            {importResult.warnings?.length > 0 && (
              <>
                <h3>Warnings</h3>
                <table>
                  <thead>
                    <tr>
                      <th>Row</th>
                      <th>Warnings</th>
                    </tr>
                  </thead>
                  <tbody>
                    {importResult.warnings.map((row) => (
                      <tr key={`warn-${row.rowNumber}`}>
                        <td>{row.rowNumber}</td>
                        <td>{(row.warnings || []).join(', ')}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </>
            )}
          </div>
        )}
      </section>

      <section className="card">
        <h2>Waybills List</h2>
        <div className="filters">
          <div className="field-row">
            <label htmlFor="statusFilter">Status</label>
            <select
              id="statusFilter"
              value={statusFilter}
              onChange={(event) => setStatusFilter(event.target.value)}
            >
              {statusOptions.map((status) => (
                <option key={status || 'ALL'} value={status}>
                  {status || 'All'}
                </option>
              ))}
            </select>
          </div>
          <div className="field-row">
            <label htmlFor="searchFilter">Search</label>
            <input
              id="searchFilter"
              type="text"
              placeholder="Project or supplier"
              value={searchFilter}
              onChange={(event) => setSearchFilter(event.target.value)}
            />
          </div>
          <button onClick={loadWaybills} disabled={waybillsLoading}>
            {waybillsLoading ? 'Loading...' : 'Load Waybills'}
          </button>
        </div>

        {waybillsError && <div className="error">{waybillsError}</div>}
        <div className="table-toolbar">
          <div className="table-meta">Total: {waybillsTotal}</div>
          <button className="ghost" onClick={() => loadWaybills({ refresh: true })} disabled={waybillsLoading}>
            {waybillsLoading ? 'Loading...' : 'Refresh'}
          </button>
        </div>
        <table>
          <thead>
            <tr>
              <th>Waybill #</th>
              <th>Project</th>
              <th>Supplier</th>
              <th>Waybill Date</th>
              <th>Delivery Date</th>
              <th>Status</th>
              <th>Total Amount</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {waybills.length === 0 && (
              <tr>
                <td colSpan="7" className="empty">
                  No data loaded.
                </td>
              </tr>
            )}
            {waybills.map((item) => (
              <tr key={item.id}>
                <td>{item.waybillNumber}</td>
                <td>{item.projectName}</td>
                <td>{item.supplierName}</td>
                <td>{item.waybillDate}</td>
                <td>{item.deliveryDate}</td>
                <td>{item.status}</td>
                <td>{item.totalAmount}</td>
                <td>
                  <button className="ghost" onClick={() => openEdit(item)}>
                    Edit
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {editTarget && editForm && (
          <div className="edit-panel">
            <div className="edit-header">
              <h3>Edit Waybill {editTarget.waybillNumber}</h3>
              <button className="ghost" onClick={closeEdit}>
                Close
              </button>
            </div>
            <div className="edit-grid">
              <label>
                Status
                <select
                  value={editForm.status}
                  onChange={(event) => handleEditChange('status', event.target.value)}
                >
                  {statusOptions.filter(Boolean).map((status) => (
                    <option key={status} value={status}>
                      {status}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Delivery Date
                <input
                  type="date"
                  value={editForm.deliveryDate}
                  onChange={(event) => handleEditChange('deliveryDate', event.target.value)}
                />
              </label>
              <label>
                Quantity
                <input
                  type="number"
                  step="0.01"
                  value={editForm.quantity}
                  onChange={(event) => handleEditChange('quantity', event.target.value)}
                />
              </label>
              <label>
                Unit Price
                <input
                  type="number"
                  step="0.01"
                  value={editForm.unitPrice}
                  onChange={(event) => handleEditChange('unitPrice', event.target.value)}
                />
              </label>
              <label>
                Total Amount
                <input
                  type="number"
                  step="0.01"
                  value={editForm.totalAmount}
                  onChange={(event) => handleEditChange('totalAmount', event.target.value)}
                />
              </label>
              <label>
                Product Code
                <input
                  type="text"
                  value={editForm.productCode}
                  onChange={(event) => handleEditChange('productCode', event.target.value)}
                />
              </label>
            </div>
            {editError && <div className="error">{editError}</div>}
            {editMessage && <div className="success">{editMessage}</div>}
            <div className="edit-actions">
              <button onClick={saveEdit} disabled={editSaving}>
                {editSaving ? 'Saving...' : 'Save'}
              </button>
            </div>
          </div>
        )}
      </section>
    </div>
  )
}

export default App
