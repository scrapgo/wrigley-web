namespace ScrapGo.Core.Modules.Suppliers.IntegrationTests.Fixtures;

/// <summary>Real Quickbase responses from the Suppliers table (bqrcgnatz), trimmed.</summary>
public static class SupplierResponses
{
    /// <summary>The detail query for record 17511.</summary>
    public const string FirstClassAutoSalvage = """
        {
          "data": [
            {
              "3": { "value": 17511 },
              "8": { "value": "1st Class Auto Salvage" },
              "9": { "value": "2350 Vulcan Rd" },
              "10": { "value": "Apopka" },
              "11": { "value": "FL" },
              "12": { "value": "32703" },
              "28": { "value": 67 },
              "64": { "value": "United States" },
              "74": { "value": { "email": "john@scrapgo.com", "id": "67648474.dyx6", "name": "john@scrapgo.com" } },
              "116": { "value": null },
              "123": { "value": [ "David Esteves " ] },
              "129": { "value": 2 },
              "133": { "value": "" },
              "214": { "value": 2 },
              "301": { "value": "pipoe720@hotmail.com" },
              "320": { "value": "Net 5" },
              "321": { "value": true },
              "346": { "value": 257 },
              "355": { "value": "(321) 356-4622" }
            }
          ],
          "fields": [],
          "metadata": { "numFields": 18, "numRecords": 1, "skip": 0, "totalRecords": 1 }
        }
        """;

    /// <summary>A record with Dead Freight (321) unchecked, i.e. Exempt.</summary>
    public const string DeadFreightUnchecked = """
        {
          "data": [ { "3": { "value": 9583 }, "8": { "value": "1st Class Auto Salvage" }, "321": { "value": false } } ],
          "fields": [],
          "metadata": { "numRecords": 1, "skip": 0, "totalRecords": 1 }
        }
        """;

    /// <summary>A record whose payment terms aren't one of the known values.</summary>
    public const string UnknownPaymentTerms = """
        {
          "data": [ { "3": { "value": 9583 }, "8": { "value": "1st Class Auto Salvage" }, "320": { "value": "Net 15" } } ],
          "fields": [],
          "metadata": { "numRecords": 1, "skip": 0, "totalRecords": 1 }
        }
        """;

    /// <summary>The call and prospect status query for record 17511 (sample values).</summary>
    public const string CallProspectStatus = """
        {
          "data": [
            {
              "3": { "value": 17511 },
              "97": { "value": "Spoke to David; call back next week." },
              "181": { "value": "2026-10-15" },
              "192": { "value": "Prospect" },
              "193": { "value": "No Answer - Voice Mail" },
              "197": { "value": "Yes" },
              "236": { "value": "Payment Terms" },
              "238": { "value": "Wants Net 5 instead of Net 10." }
            }
          ],
          "fields": [],
          "metadata": { "numRecords": 1, "skip": 0, "totalRecords": 1 }
        }
        """;

    /// <summary>A call and prospect status with dropdown text that isn't a known value, and empty fields.</summary>
    public const string CallProspectStatusUnknownChoices = """
        {
          "data": [ { "3": { "value": 9583 }, "181": { "value": "" }, "193": { "value": "Left a message" }, "236": { "value": "Too expensive" } } ],
          "fields": [],
          "metadata": { "numRecords": 1, "skip": 0, "totalRecords": 1 }
        }
        """;

    /// <summary>The yard capabilities query for record 17511 (sample values; every field a checkbox).</summary>
    public const string YardCapabilities = """
        {
          "data": [
            {
              "3": { "value": 17511 },
              "65": { "value": true },
              "78": { "value": true },
              "182": { "value": false },
              "183": { "value": false },
              "184": { "value": true },
              "185": { "value": false },
              "186": { "value": true },
              "187": { "value": false },
              "204": { "value": false },
              "205": { "value": true },
              "225": { "value": false },
              "230": { "value": false },
              "359": { "value": true }
            }
          ],
          "fields": [],
          "metadata": { "numRecords": 1, "skip": 0, "totalRecords": 1 }
        }
        """;

    /// <summary>The Target Pricing — Progress Rail query for record 17511 (sample values; some empty).</summary>
    public const string TargetPricingProgressRail = """
        {
          "data": [
            {
              "3": { "value": 17511 },
              "336": { "value": 180 },
              "337": { "value": "Net Ton" },
              "339": { "value": 2.5 },
              "340": { "value": 22 },
              "341": { "value": 1100.5 },
              "342": { "value": 205.75 },
              "345": { "value": 257 },
              "346": { "value": 257 },
              "347": { "value": 0.1285 },
              "348": { "value": 12.85 },
              "349": { "value": 287.84 },
              "352": { "value": "#1 HMS" },
              "358": { "value": "" },
              "363": { "value": -5 }
            }
          ],
          "fields": [],
          "metadata": { "numRecords": 1, "skip": 0, "totalRecords": 1 }
        }
        """;

    /// <summary>The name list query, first records.</summary>
    public const string NameList = """
        {
          "data": [
            { "3": { "value": 8701 }, "8": { "value": " C & M Car Crushing Inc" } },
            { "3": { "value": 18897 }, "8": { "value": "\"Cash For Junk Cars\" Michael's Auto & Towing" } },
            { "3": { "value": 9583 }, "8": { "value": "1st Class Auto Salvage" } },
            { "3": { "value": 17511 }, "8": { "value": "1st Class Auto Salvage" } }
          ],
          "fields": [ { "id": 3, "label": "Record ID#", "type": "recordid" }, { "id": 8, "label": "Account", "type": "text" } ],
          "metadata": { "numFields": 2, "numRecords": 4, "skip": 0, "totalRecords": 35420 }
        }
        """;

    public const string Empty = """{ "data": [], "fields": [], "metadata": { "numRecords": 0, "skip": 0, "totalRecords": 0 } }""";
}
