using FriendPeanut.Application.Abstractions;
using Npgsql;

namespace FriendPeanut.Infrastructure.Persistence;

public sealed class NpgsqlMasterDataStore : IMasterDataStore
{
    private readonly NpgsqlDataSource _ds;
    public NpgsqlMasterDataStore(NpgsqlDataSource ds) { _ds = ds; }

    // Items
    public async Task<IReadOnlyList<ItemRecord>> ListItemsAsync(int page, int pageSize, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,code,name_ar,normalized_name,category_id,base_unit_id,unit_status,lot_tracking_enabled,can_purchase,can_sell,can_manufacture,is_active,version from friend_peanut.items order by code limit @l offset @o");
        cmd.Parameters.AddWithValue("l", pageSize); cmd.Parameters.AddWithValue("o", (page - 1) * pageSize);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<ItemRecord>();
        while (await r.ReadAsync(ct)) list.Add(ReadItem(r));
        return list;
    }
    public async Task<ItemRecord?> FindItemAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,code,name_ar,normalized_name,category_id,base_unit_id,unit_status,lot_tracking_enabled,can_purchase,can_sell,can_manufacture,is_active,version from friend_peanut.items where id=@id");
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? ReadItem(r) : null;
    }
    public async Task<Guid> CreateItemAsync(ItemRecord item, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using var cmd = _ds.CreateCommand("insert into friend_peanut.items (id,code,name_ar,normalized_name,category_id,base_unit_id,unit_status,lot_tracking_enabled,can_purchase,can_sell,can_manufacture,is_active) values (@id,@c,@n,@nn,@cat,@bu,@us,@lt,@p,@s,@m,@a)");
        cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("c", item.Code); cmd.Parameters.AddWithValue("n", item.NameAr);
        cmd.Parameters.AddWithValue("nn", item.NormalizedName); cmd.Parameters.AddWithValue("cat", item.CategoryId);
        cmd.Parameters.AddWithValue("bu", (object?)item.BaseUnitId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("us", item.UnitStatus);
        cmd.Parameters.AddWithValue("lt", item.LotTrackingEnabled);
        cmd.Parameters.AddWithValue("p", item.CanPurchase); cmd.Parameters.AddWithValue("s", item.CanSell); cmd.Parameters.AddWithValue("m", item.CanManufacture);
        cmd.Parameters.AddWithValue("a", item.IsActive);
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }
    public async Task UpdateItemAsync(ItemRecord item, long expectedVersion, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("update friend_peanut.items set name_ar=@n, normalized_name=@nn, category_id=@cat, base_unit_id=@bu, unit_status=@us, can_purchase=@p, can_sell=@s, can_manufacture=@m, version=version+1 where id=@id and version=@v");
        cmd.Parameters.AddWithValue("id", item.Id); cmd.Parameters.AddWithValue("n", item.NameAr); cmd.Parameters.AddWithValue("nn", item.NormalizedName);
        cmd.Parameters.AddWithValue("cat", item.CategoryId); cmd.Parameters.AddWithValue("bu", (object?)item.BaseUnitId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("us", item.UnitStatus); cmd.Parameters.AddWithValue("p", item.CanPurchase); cmd.Parameters.AddWithValue("s", item.CanSell); cmd.Parameters.AddWithValue("m", item.CanManufacture);
        cmd.Parameters.AddWithValue("v", expectedVersion);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) throw new InvalidOperationException("CONFLICT_VERSION");
    }
    public async Task DeactivateItemAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("update friend_peanut.items set is_active=false, version=version+1 where id=@id");
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }
    public async Task<IReadOnlyList<ItemRecord>> SearchItemsAsync(string query, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select distinct i.id,i.code,i.name_ar,i.normalized_name,i.category_id,i.base_unit_id,i.unit_status,i.lot_tracking_enabled,i.can_purchase,i.can_sell,i.can_manufacture,i.is_active,i.version from friend_peanut.items i left join friend_peanut.item_aliases a on a.item_id=i.id where i.normalized_name like @q or a.normalized_alias like @q order by i.code limit 50");
        cmd.Parameters.AddWithValue("q", "%" + query + "%");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<ItemRecord>();
        while (await r.ReadAsync(ct)) list.Add(ReadItem(r));
        return list;
    }

    // Categories
    public async Task<IReadOnlyList<CategoryRecord>> ListCategoriesAsync(CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,code,name_ar,parent_id,is_active,version from friend_peanut.item_categories order by code");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<CategoryRecord>();
        while (await r.ReadAsync(ct)) list.Add(new CategoryRecord(r.GetGuid(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.GetBoolean(4), r.GetInt64(5)));
        return list;
    }
    public async Task<CategoryRecord?> FindCategoryAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,code,name_ar,parent_id,is_active,version from friend_peanut.item_categories where id=@id");
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new CategoryRecord(r.GetGuid(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.GetBoolean(4), r.GetInt64(5)) : null;
    }
    public async Task<Guid> CreateCategoryAsync(CategoryRecord c, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using var cmd = _ds.CreateCommand("insert into friend_peanut.item_categories (id,code,name_ar,parent_id) values (@id,@c,@n,@p)");
        cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("c", c.Code); cmd.Parameters.AddWithValue("n", c.NameAr);
        cmd.Parameters.AddWithValue("p", (object?)c.ParentId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }
    public async Task UpdateCategoryAsync(CategoryRecord c, long expectedVersion, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("update friend_peanut.item_categories set name_ar=@n, parent_id=@p, version=version+1 where id=@id and version=@v");
        cmd.Parameters.AddWithValue("id", c.Id); cmd.Parameters.AddWithValue("n", c.NameAr);
        cmd.Parameters.AddWithValue("p", (object?)c.ParentId ?? DBNull.Value); cmd.Parameters.AddWithValue("v", expectedVersion);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) throw new InvalidOperationException("CONFLICT_VERSION");
    }

    // Units
    public async Task<IReadOnlyList<UnitRecord>> ListUnitsAsync(CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,code,name_ar,dimension,is_active,version from friend_peanut.units order by code");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<UnitRecord>();
        while (await r.ReadAsync(ct)) list.Add(new UnitRecord(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetBoolean(4), r.GetInt64(5)));
        return list;
    }
    public async Task<UnitRecord?> FindUnitAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,code,name_ar,dimension,is_active,version from friend_peanut.units where id=@id");
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new UnitRecord(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetBoolean(4), r.GetInt64(5)) : null;
    }
    public async Task<Guid> CreateUnitAsync(UnitRecord u, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using var cmd = _ds.CreateCommand("insert into friend_peanut.units (id,code,name_ar,dimension) values (@id,@c,@n,@d)");
        cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("c", u.Code); cmd.Parameters.AddWithValue("n", u.NameAr); cmd.Parameters.AddWithValue("d", u.Dimension);
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }
    public async Task UpdateUnitAsync(UnitRecord u, long expectedVersion, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("update friend_peanut.units set name_ar=@n, dimension=@d, version=version+1 where id=@id and version=@v");
        cmd.Parameters.AddWithValue("id", u.Id); cmd.Parameters.AddWithValue("n", u.NameAr); cmd.Parameters.AddWithValue("d", u.Dimension); cmd.Parameters.AddWithValue("v", expectedVersion);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) throw new InvalidOperationException("CONFLICT_VERSION");
    }

    // Conversions
    public async Task<IReadOnlyList<UnitConversionRecord>> ListConversionsAsync(CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,item_id,from_unit_id,to_unit_id,factor,valid_from,valid_to,version from friend_peanut.unit_conversions order by item_id");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<UnitConversionRecord>();
        while (await r.ReadAsync(ct)) list.Add(new UnitConversionRecord(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetGuid(3), r.GetDecimal(4), r.GetFieldValue<DateOnly>(5), r.IsDBNull(6) ? null : r.GetFieldValue<DateOnly>(6), r.GetInt64(7)));
        return list;
    }
    public async Task<UnitConversionRecord?> FindConversionAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,item_id,from_unit_id,to_unit_id,factor,valid_from,valid_to,version from friend_peanut.unit_conversions where id=@id");
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new UnitConversionRecord(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetGuid(3), r.GetDecimal(4), r.GetFieldValue<DateOnly>(5), r.IsDBNull(6) ? null : r.GetFieldValue<DateOnly>(6), r.GetInt64(7)) : null;
    }
    public async Task<Guid> CreateConversionAsync(UnitConversionRecord c, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using var cmd = _ds.CreateCommand("insert into friend_peanut.unit_conversions (id,item_id,from_unit_id,to_unit_id,factor,valid_from,valid_to) values (@id,@i,@f,@t,@fa,@vf,@vt)");
        cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("i", c.ItemId); cmd.Parameters.AddWithValue("f", c.FromUnitId); cmd.Parameters.AddWithValue("t", c.ToUnitId);
        cmd.Parameters.AddWithValue("fa", c.Factor); cmd.Parameters.AddWithValue("vf", c.ValidFrom); cmd.Parameters.AddWithValue("vt", (object?)c.ValidTo ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }
    public async Task UpdateConversionAsync(UnitConversionRecord c, long expectedVersion, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("update friend_peanut.unit_conversions set factor=@fa, valid_from=@vf, valid_to=@vt, version=version+1 where id=@id and version=@v");
        cmd.Parameters.AddWithValue("id", c.Id); cmd.Parameters.AddWithValue("fa", c.Factor); cmd.Parameters.AddWithValue("vf", c.ValidFrom);
        cmd.Parameters.AddWithValue("vt", (object?)c.ValidTo ?? DBNull.Value); cmd.Parameters.AddWithValue("v", expectedVersion);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) throw new InvalidOperationException("CONFLICT_VERSION");
    }

    // Warehouses
    public async Task<IReadOnlyList<WarehouseRecord>> ListWarehousesAsync(CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,code,name_ar,site_id,factory_id,version from friend_peanut.warehouses order by code");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<WarehouseRecord>();
        while (await r.ReadAsync(ct)) list.Add(new WarehouseRecord(r.GetGuid(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.IsDBNull(4) ? null : r.GetGuid(4), r.GetInt64(5)));
        return list;
    }
    public async Task<WarehouseRecord?> FindWarehouseAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,code,name_ar,site_id,factory_id,version from friend_peanut.warehouses where id=@id");
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new WarehouseRecord(r.GetGuid(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.IsDBNull(4) ? null : r.GetGuid(4), r.GetInt64(5)) : null;
    }

    // Parties
    public async Task<IReadOnlyList<PartyRecord>> ListPartiesAsync(CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,code,name_ar,phone,address,tax_number,is_active,version from friend_peanut.parties order by code");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<PartyRecord>();
        while (await r.ReadAsync(ct)) list.Add(ReadParty(r));
        return list;
    }
    public async Task<PartyRecord?> FindPartyAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,code,name_ar,phone,address,tax_number,is_active,version from friend_peanut.parties where id=@id");
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? ReadParty(r) : null;
    }
    public async Task<Guid> CreatePartyAsync(PartyRecord p, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using var cmd = _ds.CreateCommand("insert into friend_peanut.parties (id,code,name_ar,phone,address,tax_number) values (@id,@c,@n,@ph,@ad,@tx)");
        cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("c", p.Code); cmd.Parameters.AddWithValue("n", p.NameAr);
        cmd.Parameters.AddWithValue("ph", (object?)p.Phone ?? DBNull.Value); cmd.Parameters.AddWithValue("ad", (object?)p.Address ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tx", (object?)p.TaxNumber ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }
    public async Task UpdatePartyAsync(PartyRecord p, long expectedVersion, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("update friend_peanut.parties set name_ar=@n, phone=@ph, address=@ad, tax_number=@tx, version=version+1 where id=@id and version=@v");
        cmd.Parameters.AddWithValue("id", p.Id); cmd.Parameters.AddWithValue("n", p.NameAr);
        cmd.Parameters.AddWithValue("ph", (object?)p.Phone ?? DBNull.Value); cmd.Parameters.AddWithValue("ad", (object?)p.Address ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tx", (object?)p.TaxNumber ?? DBNull.Value); cmd.Parameters.AddWithValue("v", expectedVersion);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) throw new InvalidOperationException("CONFLICT_VERSION");
    }

    // Cost centers
    public async Task<IReadOnlyList<CostCenterRecord>> ListCostCentersAsync(CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,code,name_ar,parent_id,is_active,version from friend_peanut.cost_centers order by code");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<CostCenterRecord>();
        while (await r.ReadAsync(ct)) list.Add(new CostCenterRecord(r.GetGuid(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.GetBoolean(4), r.GetInt64(5)));
        return list;
    }
    public async Task<CostCenterRecord?> FindCostCenterAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,code,name_ar,parent_id,is_active,version from friend_peanut.cost_centers where id=@id");
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new CostCenterRecord(r.GetGuid(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.GetBoolean(4), r.GetInt64(5)) : null;
    }
    public async Task<Guid> CreateCostCenterAsync(CostCenterRecord c, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using var cmd = _ds.CreateCommand("insert into friend_peanut.cost_centers (id,code,name_ar,parent_id) values (@id,@c,@n,@p)");
        cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("c", c.Code); cmd.Parameters.AddWithValue("n", c.NameAr);
        cmd.Parameters.AddWithValue("p", (object?)c.ParentId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }
    public async Task UpdateCostCenterAsync(CostCenterRecord c, long expectedVersion, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("update friend_peanut.cost_centers set name_ar=@n, parent_id=@p, version=version+1 where id=@id and version=@v");
        cmd.Parameters.AddWithValue("id", c.Id); cmd.Parameters.AddWithValue("n", c.NameAr);
        cmd.Parameters.AddWithValue("p", (object?)c.ParentId ?? DBNull.Value); cmd.Parameters.AddWithValue("v", expectedVersion);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) throw new InvalidOperationException("CONFLICT_VERSION");
    }

    // Lots
    public async Task<IReadOnlyList<LotRecord>> ListLotsAsync(CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,item_id,code,is_system_untracked,received_date,version from friend_peanut.lots order by code");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<LotRecord>();
        while (await r.ReadAsync(ct)) list.Add(new LotRecord(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetBoolean(3), r.IsDBNull(4) ? null : r.GetFieldValue<DateOnly>(4), r.GetInt64(5)));
        return list;
    }
    public async Task<LotRecord?> FindLotAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,item_id,code,is_system_untracked,received_date,version from friend_peanut.lots where id=@id");
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new LotRecord(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetBoolean(3), r.IsDBNull(4) ? null : r.GetFieldValue<DateOnly>(4), r.GetInt64(5)) : null;
    }
    public async Task<Guid> CreateLotAsync(LotRecord l, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using var cmd = _ds.CreateCommand("insert into friend_peanut.lots (id,item_id,code,is_system_untracked,received_date) values (@id,@i,@c,@u,@r)");
        cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("i", l.ItemId); cmd.Parameters.AddWithValue("c", l.Code);
        cmd.Parameters.AddWithValue("u", l.IsSystemUntracked); cmd.Parameters.AddWithValue("r", (object?)l.ReceivedDate ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }
    public async Task UpdateLotAsync(LotRecord l, long expectedVersion, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("update friend_peanut.lots set code=@c, received_date=@r, version=version+1 where id=@id and version=@v");
        cmd.Parameters.AddWithValue("id", l.Id); cmd.Parameters.AddWithValue("c", l.Code);
        cmd.Parameters.AddWithValue("r", (object?)l.ReceivedDate ?? DBNull.Value); cmd.Parameters.AddWithValue("v", expectedVersion);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) throw new InvalidOperationException("CONFLICT_VERSION");
    }

    // Aliases
    public async Task AddItemAliasAsync(Guid itemId, string aliasAr, string normalizedAlias, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("insert into friend_peanut.item_aliases (item_id,alias_ar,normalized_alias) values (@i,@a,@na)");
        cmd.Parameters.AddWithValue("i", itemId); cmd.Parameters.AddWithValue("a", aliasAr); cmd.Parameters.AddWithValue("na", normalizedAlias);
        await cmd.ExecuteNonQueryAsync(ct);
    }
    public async Task<IReadOnlyList<ItemAliasRecord>> ListItemAliasesAsync(Guid itemId, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,item_id,alias_ar,normalized_alias from friend_peanut.item_aliases where item_id=@i order by alias_ar");
        cmd.Parameters.AddWithValue("i", itemId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<ItemAliasRecord>();
        while (await r.ReadAsync(ct)) list.Add(new ItemAliasRecord(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3)));
        return list;
    }

    private static ItemRecord ReadItem(NpgsqlDataReader r) => new(
        r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetGuid(4),
        r.IsDBNull(5) ? null : r.GetGuid(5), r.GetString(6), r.GetBoolean(7), r.GetBoolean(8),
        r.GetBoolean(9), r.GetBoolean(10), r.GetBoolean(11), r.GetInt64(12));

    private static PartyRecord ReadParty(NpgsqlDataReader r) => new(
        r.GetGuid(0), r.GetString(1), r.GetString(2),
        r.IsDBNull(3) ? null : r.GetString(3),
        r.IsDBNull(4) ? null : r.GetString(4),
        r.IsDBNull(5) ? null : r.GetString(5),
        r.GetBoolean(6), r.GetInt64(7));
}