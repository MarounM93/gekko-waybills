using CsvHelper.Configuration;

namespace Gekko.Waybills.Application.Imports;

/// <summary>CSV mapping for <see cref="ImportRowDto"/> with flexible headers.</summary>
public sealed class ImportRowDtoMap : ClassMap<ImportRowDto>
{
    public ImportRowDtoMap()
    {
        Map(m => m.TenantId).Name("tenant_id", "tenantid", "tenant");
        Map(m => m.WaybillNumber)
            .Name("waybill_number", "waybillNumber", "waybill_id", "waybillId", "waybill")
            .Convert(row =>
                (row.Row.GetField("waybill_number")
                 ?? row.Row.GetField("waybillNumber")
                 ?? row.Row.GetField("waybill_id")
                 ?? row.Row.GetField("waybillId")
                 ?? row.Row.GetField("waybill"))
                ?.Trim());
        Map(m => m.ProjectName).Name("project_name", "projectname", "project");
        Map(m => m.SupplierName).Name("supplier_name", "suppliername", "supplier");
        Map(m => m.WaybillDate).Name("waybill_date", "waybilldate", "waybill date");
        Map(m => m.DeliveryDate).Name("delivery_date", "deliverydate", "delivery date");
        Map(m => m.ProductCode).Name("product_code", "productcode", "product");
        Map(m => m.Quantity).Name("quantity", "qty");
        Map(m => m.UnitPrice).Name("unit_price", "unitprice", "price");
        Map(m => m.TotalAmount).Name("total_amount", "totalamount", "total");
        Map(m => m.Status).Name("status", "waybill_status");
    }
}
