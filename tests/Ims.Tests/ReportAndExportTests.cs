using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Ims.Api.Data;
using Ims.Api.Domain;
using Ims.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Ims.Tests;

/// <summary>Day 4: the three reports and the CSV / Excel exports.</summary>
[Collection(ApiCollection.Name)]
public class ReportAndExportTests(ImsApiFactory factory)
{
    private const string SeededUnit = "2440701AK2057";

    private async Task<HttpClient> SignInAsync(string user)
    {
        var client = await ApiTestHelpers.CreateClientAsync(factory);
        (await ApiTestHelpers.LoginAsync(client, user, user == "admin" ? "Admin@12345" : "Demo@12345")).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private long IdOf<T>(string name) where T : MasterDataEntity
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ImsDbContext>().Set<T>().Where(x => x.Name == name).Select(x => x.Id).First();
    }

    /// <summary>Receives a brand-new unit at the given receive point and returns its barcode.</summary>
    private async Task<string> ReceiveNewUnitAsync(string point, string product = "Refrigerator")
    {
        var code = "S" + Guid.NewGuid().ToString("N")[..11].ToUpperInvariant();
        var receiver = await SignInAsync("receiver");
        (await receiver.PostAsJsonAsync("/api/ops/receive", new { barcode = code, productId = IdOf<Product>(product), receivePointId = IdOf<Point>(point) }))
            .EnsureSuccessStatusCode();
        return code;
    }

    // ---- stock report ----

    [Fact]
    public async Task Stock_report_lists_units_in_stock_with_their_receive_point_and_leaves_out_delivered_ones()
    {
        var viewer = await SignInAsync("viewer");
        var inStock = await ReceiveNewUnitAsync("Refrigerator Receive Point 2", "Freezer");

        var report = await Json(await viewer.GetAsync($"/api/reports/stock?q={inStock}"));

        Assert.Equal(1, report.GetProperty("totalCount").GetInt32());
        var row = report.GetProperty("items")[0];
        Assert.Equal(inStock, row.GetProperty("barcode").GetString());
        Assert.Equal("Freezer", row.GetProperty("productName").GetString());
        Assert.Equal("Refrigerator Receive Point 2", row.GetProperty("pointName").GetString());
        Assert.Equal(0, row.GetProperty("daysInStock").GetInt32());
        Assert.Equal(0, row.GetProperty("componentCount").GetInt32());

        // The delivered sample unit is not in stock.
        var sample = await Json(await viewer.GetAsync($"/api/reports/stock?q={SeededUnit}"));
        Assert.Equal(0, sample.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Stock_report_total_matches_the_dashboard_and_the_chips_add_up()
    {
        await ReceiveNewUnitAsync("Refrigerator Receive Point 1");
        var admin = await SignInAsync("admin");

        var stock = await Json(await admin.GetAsync("/api/reports/stock?pageSize=200"));
        var dashboard = (await Json(await admin.GetAsync("/api/dashboard"))).GetProperty("stockCount").GetInt32();

        Assert.Equal(dashboard, stock.GetProperty("totalCount").GetInt32());
        Assert.Equal(dashboard, stock.GetProperty("byPoint").EnumerateArray().Sum(p => p.GetProperty("count").GetInt32()));
    }

    [Fact]
    public async Task Stock_report_filters_by_product_point_and_minimum_days()
    {
        var code = await ReceiveNewUnitAsync("Refrigerator Receive Point 2", "Air Conditioner");
        var viewer = await SignInAsync("viewer");
        var ac = IdOf<Product>("Air Conditioner");
        var point2 = IdOf<Point>("Refrigerator Receive Point 2");
        var point1 = IdOf<Point>("Refrigerator Receive Point 1");

        Assert.Equal(1, (await Json(await viewer.GetAsync($"/api/reports/stock?q={code}&productId={ac}"))).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await Json(await viewer.GetAsync($"/api/reports/stock?q={code}&productId={IdOf<Product>("Freezer")}"))).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, (await Json(await viewer.GetAsync($"/api/reports/stock?q={code}&pointId={point2}"))).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await Json(await viewer.GetAsync($"/api/reports/stock?q={code}&pointId={point1}"))).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await Json(await viewer.GetAsync($"/api/reports/stock?q={code}&minDays=1"))).GetProperty("totalCount").GetInt32()); // received just now
    }

    // ---- summary report ----

    [Fact]
    public async Task Summary_for_the_sample_week_counts_one_tracked_one_received_one_delivered()
    {
        var viewer = await SignInAsync("viewer");

        var report = await Json(await viewer.GetAsync("/api/reports/summary?from=2026-03-27&to=2026-03-30"));

        var totals = report.GetProperty("totals");
        Assert.Equal(1, totals.GetProperty("tracked").GetInt32());
        Assert.Equal(1, totals.GetProperty("received").GetInt32());
        Assert.Equal(1, totals.GetProperty("delivered").GetInt32());
        Assert.Equal(0, totals.GetProperty("reversed").GetInt32());
        Assert.Equal(4, report.GetProperty("chart").GetArrayLength()); // one entry per day, zeros included
        Assert.Equal(1, report.GetProperty("chart")[0].GetProperty("tracked").GetInt32()); // 27 March
        Assert.Equal(1, report.GetProperty("chart")[1].GetProperty("received").GetInt32()); // 28 March
        Assert.Equal(1, report.GetProperty("chart")[3].GetProperty("delivered").GetInt32()); // 30 March
    }

    [Fact]
    public async Task Summary_filters_by_point_and_product()
    {
        var admin = await SignInAsync("admin");
        var other = IdOf<Point>("Refrigerator Receive Point 2");
        var fridge = IdOf<Product>("Refrigerator");
        var freezer = IdOf<Product>("Freezer");

        var onOtherPoint = await Json(await admin.GetAsync($"/api/reports/summary?from=2026-03-27&to=2026-03-30&pointId={other}"));
        Assert.Equal(0, onOtherPoint.GetProperty("totals").GetProperty("received").GetInt32());

        var fridges = await Json(await admin.GetAsync($"/api/reports/summary?from=2026-03-27&to=2026-03-30&productId={fridge}"));
        Assert.Equal(3, fridges.GetProperty("totals").Sum());
        var freezers = await Json(await admin.GetAsync($"/api/reports/summary?from=2026-03-27&to=2026-03-30&productId={freezer}"));
        Assert.Equal(0, freezers.GetProperty("totals").Sum());
    }

    [Theory]
    [InlineData("from=2026-03-30&to=2026-03-27", "from")]  // from after to
    [InlineData("from=2025-01-01&to=2026-03-01", "to")]    // more than 366 days
    public async Task Summary_rejects_a_bad_range_with_a_field_error(string query, string field)
    {
        var viewer = await SignInAsync("viewer");

        var response = await viewer.GetAsync($"/api/reports/summary?{query}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Summary_without_dates_defaults_to_the_last_30_days()
    {
        var viewer = await SignInAsync("viewer");

        var report = await Json(await viewer.GetAsync("/api/reports/summary"));

        Assert.Equal(30, report.GetProperty("totalCount").GetInt32());
        Assert.Equal(30, report.GetProperty("chart").GetArrayLength());
    }

    // ---- barcode history ----

    [Fact]
    public async Task Barcode_history_of_the_sample_unit_shows_all_three_rows_oldest_first()
    {
        var viewer = await SignInAsync("viewer");

        var report = await Json(await viewer.GetAsync($"/api/reports/barcode?barcode={SeededUnit.ToLowerInvariant()}")); // case-insensitive

        Assert.True(report.GetProperty("found").GetBoolean());
        Assert.Equal("Delivered", report.GetProperty("unit").GetProperty("status").GetString());
        Assert.Equal("Refrigerator", report.GetProperty("unit").GetProperty("product").GetString());
        var timeline = report.GetProperty("timeline").EnumerateArray().ToList();
        Assert.Equal(["COMPONENT_TRACKED", "RECEIVED", "DELIVERED"], timeline.Select(r => r.GetProperty("eventType").GetString()!).ToArray());
    }

    [Fact]
    public async Task Barcode_history_accepts_a_component_barcode_and_unknown_barcodes_are_not_found()
    {
        var viewer = await SignInAsync("viewer");

        var byComponent = await Json(await viewer.GetAsync("/api/reports/barcode?barcode=94A600321852"));
        Assert.Equal(SeededUnit, byComponent.GetProperty("unit").GetProperty("barcode").GetString());
        Assert.Equal(3, byComponent.GetProperty("timeline").GetArrayLength());

        var unknown = await Json(await viewer.GetAsync("/api/reports/barcode?barcode=NOSUCH-CODE-1"));
        Assert.False(unknown.GetProperty("found").GetBoolean());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await viewer.GetAsync("/api/reports/barcode?barcode=x")).StatusCode);
    }

    // ---- policies ----

    [Theory]
    [InlineData("receiver")]
    [InlineData("tracker")]
    [InlineData("deliverer")]
    public async Task Operational_roles_cannot_open_reports_or_exports(string user)
    {
        var client = await SignInAsync(user);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/reports/stock")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/reports/stock?format=csv")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/reports/summary?format=xlsx")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/history?format=csv")).StatusCode);
    }

    // ---- exports ----

    private static string Text(byte[] bytes) => new UTF8Encoding(false).GetString(bytes).TrimStart('﻿');

    [Fact]
    public async Task History_csv_is_UTF8_with_BOM_a_header_row_and_honours_the_filters()
    {
        var viewer = await SignInAsync("viewer");

        var response = await viewer.GetAsync($"/api/history?format=csv&eventType=RECEIVED&q={SeededUnit}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Matches(@"^history_\d{8}-\d{4}\.csv$", response.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]); // BOM, so Excel reads UTF-8
        var lines = Text(bytes).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Time,Barcode,Product,Event,Point", lines[0]);
        Assert.Equal(2, lines.Length); // header + the one RECEIVED row of the sample unit
        Assert.StartsWith($"2026-03-28 10:15:00,{SeededUnit},Refrigerator,RECEIVED,Refrigerator Receive Point 1", lines[1]); // LOCAL time, as on screen
    }

    [Fact]
    public async Task History_xlsx_opens_in_ClosedXML_with_a_bold_frozen_header_and_the_filtered_rows()
    {
        var admin = await SignInAsync("admin");

        var response = await admin.GetAsync($"/api/history?format=xlsx&q={SeededUnit}");

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType!.MediaType);
        using var workbook = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        var sheet = workbook.Worksheet(1);
        Assert.Equal("Time", sheet.Cell(1, 1).GetString());
        Assert.True(sheet.Cell(1, 1).Style.Font.Bold);
        Assert.Equal(1, sheet.SheetView.SplitRow); // frozen header row
        Assert.True(sheet.AutoFilter.IsEnabled);
        Assert.Equal(4, sheet.RangeUsed()!.RowCount()); // header + 3 sample rows
        Assert.Equal(SeededUnit, sheet.Cell(2, 2).GetString());
    }

    [Fact]
    public async Task Export_ignores_paging_and_returns_every_matching_row()
    {
        var viewer = await SignInAsync("viewer");
        var total = (await Json(await viewer.GetAsync("/api/history?pageSize=1"))).GetProperty("totalCount").GetInt32();

        var csv = await viewer.GetAsync("/api/history?format=csv&pageSize=1&page=2");

        var lines = Text(await csv.Content.ReadAsByteArrayAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(total + 1, lines.Length);
    }

    [Fact]
    public async Task Stock_summary_and_barcode_reports_export_to_both_formats()
    {
        var viewer = await SignInAsync("viewer");

        foreach (var (url, firstHeader) in new[]
        {
            ("/api/reports/stock", "Barcode"), ("/api/reports/summary?from=2026-03-27&to=2026-03-30", "Date"), ($"/api/reports/barcode?barcode={SeededUnit}", "Time"),
        })
        {
            var csv = await viewer.GetAsync($"{url}{(url.Contains('?') ? '&' : '?')}format=csv");
            Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
            Assert.StartsWith(firstHeader, Text(await csv.Content.ReadAsByteArrayAsync()));

            var xlsx = await viewer.GetAsync($"{url}{(url.Contains('?') ? '&' : '?')}format=xlsx");
            Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
            using var workbook = new XLWorkbook(new MemoryStream(await xlsx.Content.ReadAsByteArrayAsync()));
            Assert.Equal(firstHeader, workbook.Worksheet(1).Cell(1, 1).GetString());
        }

        // The summary export has one row per day.
        var summary = await viewer.GetAsync("/api/reports/summary?from=2026-03-27&to=2026-03-30&format=csv");
        Assert.Equal(5, Text(await summary.Content.ReadAsByteArrayAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task Admin_lists_export_with_the_same_filters_as_the_screen()
    {
        var admin = await SignInAsync("admin");

        var points = Text(await (await admin.GetAsync("/api/points?format=csv&type=DELIVERY")).Content.ReadAsByteArrayAsync());
        Assert.Contains("Refrigerator Delivery Point 1", points);
        Assert.DoesNotContain("Receive Point", points);

        var users = Text(await (await admin.GetAsync("/api/users?format=csv&role=REPORT_VIEWER")).Content.ReadAsByteArrayAsync());
        Assert.Contains("viewer", users);
        Assert.DoesNotContain("tracker", users);
        Assert.DoesNotContain("hash", users, StringComparison.OrdinalIgnoreCase); // never any password data

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/products?format=xlsx")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/components?format=csv")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/products?format=pdf")).StatusCode);
    }

    // ---- ExportService itself (no HTTP) ----

    [Fact]
    public void Csv_follows_RFC_4180_quoting_and_neutralises_formulas()
    {
        var table = new ExportTable("t", ["A", "B"], [
            new object?[] { "plain", "has,comma" },
            new object?[] { "has \"quote\"", "two\r\nlines" },
            new object?[] { "=1+1", 42 },
        ]);

        var csv = Text(ExportService.ToCsv(table));

        Assert.Equal("A,B\r\nplain,\"has,comma\"\r\n\"has \"\"quote\"\"\",\"two\r\nlines\"\r\n'=1+1,42\r\n", csv);
    }
}

internal static class JsonExtensions
{
    /// <summary>Adds up the four numbers of a summary "totals" object.</summary>
    public static int Sum(this JsonElement totals) => totals.EnumerateObject().Sum(p => p.Value.GetInt32());
}
