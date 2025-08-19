using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using System.Text;

public class DatasetService {
    private readonly string dataPath;
    public DatasetService() {
        dataPath = Environment.GetEnvironmentVariable("DATA_DIR") ?? "/data";
        Directory.CreateDirectory(dataPath);
    }
    public string ProcessedPath => Path.Combine(dataPath, "processed.csv");

    public async Task<Metadata> ParseAndSaveAsync(Stream csvStream, string originalFileName) {
        // read CSV into memory, ensure Response column present, add synthetic_timestamp if missing
        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord.ToList();
        if(!headers.Contains("Response")) throw new Exception("CSV must contain 'Response' column.");
        bool hasTimestamp = headers.Contains("synthetic_timestamp");
        var records = new List<string[]>();
        var headerCount = headers.Count;
        while(csv.Read()){
            var row = new string[headerCount];
            for(int i=0;i<headerCount;i++) row[i] = csv.GetField(i);
            records.Add(row);
        }
        // create output headers
        var outHeaders = new List<string>(headers);
        if(!hasTimestamp) outHeaders.Add("synthetic_timestamp");

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", outHeaders));
        DateTime start = DateTime.Parse("2021-01-01T00:00:00Z").ToUniversalTime();
        for(int i=0;i<records.Count;i++){
            var row = records[i].ToList();
            if(!hasTimestamp) row.Add(start.AddSeconds(i).ToString("o"));
            sb.AppendLine(string.Join(",", row.Select(v => v?.Replace("\"","\"\""))));
        }
        await File.WriteAllTextAsync(ProcessedPath, sb.ToString());
        // compute metadata
        var totalRecords = records.Count;
        var totalColumns = outHeaders.Count;
        var passCount = GetPassCount(ProcessedPath);
        return new Metadata {
            FileName = originalFileName,
            TotalRecords = totalRecords,
            TotalColumns = totalColumns,
            PassRate = Math.Round(100.0 * passCount / Math.Max(1, totalRecords), 2),
            FirstTimestamp = start.ToString("o"),
            LastTimestamp = start.AddSeconds(totalRecords-1).ToString("o")
        };
    }

    private int GetPassCount(string path) {
        using var sr = new StreamReader(path);
        var header = sr.ReadLine();
        var idx = header.Split(',').ToList().FindIndex(h => h=="Response");
        int cnt=0;
        while(!sr.EndOfStream){
            var line = sr.ReadLine();
            if(string.IsNullOrEmpty(line)) continue;
            var cols = line.Split(',');
            if(cols.Length>idx && cols[idx]=="1") cnt++;
        }
        return cnt;
    }
}

public class Metadata {
    public string FileName { get; set; }
    public int TotalRecords { get; set; }
    public int TotalColumns { get; set; }
    public double PassRate { get; set; }
    public string FirstTimestamp { get; set; }
    public string LastTimestamp { get; set; }
}
