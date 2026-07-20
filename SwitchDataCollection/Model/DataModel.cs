using System;
using System.Collections.Generic;
using System.Linq;

namespace SwitchDataCollection.Model
{
    public class DataRecord
    {
        public int RowIndex { get; set; }
        public Dictionary<string, object> Fields { get; set; }
        public DateTime Timestamp { get; set; }

        public DataRecord()
        {
            Fields = new Dictionary<string, object>();
            Timestamp = DateTime.Now;
        }

        public object this[string key]
        {
            get => Fields.ContainsKey(key) ? Fields[key] : null;
            set => Fields[key] = value;
        }

        public T GetValue<T>(string key)
        {
            if (!Fields.ContainsKey(key)) return default(T);
            try
            {
                return (T)Convert.ChangeType(Fields[key], typeof(T));
            }
            catch
            {
                return default(T);
            }
        }

        public override string ToString()
        {
            string fields = string.Join(", ", Fields.Select(kv => $"{kv.Key}={kv.Value}"));
            return $"Row[{RowIndex}] {fields}";
        }
    }

    public class DataBatch
    {
        public string SourceFileName { get; set; }
        public DateTime ProcessTime { get; set; }
        public int TotalRecords { get; set; }
        public List<DataRecord> Records { get; set; }
        public Dictionary<string, string> Metadata { get; set; }

        public DataBatch()
        {
            Records = new List<DataRecord>();
            Metadata = new Dictionary<string, string>();
            ProcessTime = DateTime.Now;
        }

        public void AddRecord(DataRecord record)
        {
            Records.Add(record);
            TotalRecords = Records.Count;
        }
    }

    public class PlcDataPacket
    {
        public int PacketId { get; set; }
        public DateTime SendTime { get; set; }
        public int DataLength { get; set; }
        public byte[] RawData { get; set; }
        public Dictionary<string, object> ParsedData { get; set; }

        public PlcDataPacket()
        {
            ParsedData = new Dictionary<string, object>();
            SendTime = DateTime.Now;
        }
    }

    public static class DataConverter
    {
        public static List<DataRecord> ConvertFromDictionaryList(List<Dictionary<string, object>> dictList)
        {
            var records = new List<DataRecord>();
            for (int i = 0; i < dictList.Count; i++)
            {
                var record = new DataRecord { RowIndex = i };
                foreach (var kv in dictList[i])
                {
                    record.Fields[kv.Key] = kv.Value;
                }
                records.Add(record);
            }
            return records;
        }

        public static DataBatch CreateBatch(string sourceFileName, List<Dictionary<string, object>> dictList)
        {
            var batch = new DataBatch { SourceFileName = sourceFileName };
            batch.Metadata["Source"] = sourceFileName;
            batch.Metadata["RecordCount"] = dictList.Count.ToString();
            batch.Metadata["ProcessTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (var record in ConvertFromDictionaryList(dictList))
            {
                batch.AddRecord(record);
            }

            return batch;
        }

        public static List<DataRecord> CreateRecords(List<Dictionary<string, object>> dictList)
        {
            return ConvertFromDictionaryList(dictList);
        }
    }
}
