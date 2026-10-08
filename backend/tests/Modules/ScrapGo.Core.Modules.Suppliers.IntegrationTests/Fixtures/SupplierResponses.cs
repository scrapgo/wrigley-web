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

    /// <summary>A record with Dead Freight (321) unchecked.</summary>
    public const string NotDeadFreight = """
        {
          "data": [ { "3": { "value": 9583 }, "8": { "value": "1st Class Auto Salvage" }, "321": { "value": false } } ],
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
