using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AIUnbound.Editor
{
    public static class Phase2Validation
    {
        [MenuItem("AI Unbound/Validate Phase 2 Data")]
        public static void Run()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            ValidateCsv(Path.Combine(projectRoot, "docs/preproduction/CHARACTER_ROSTER.csv"), "id,name,role,grade");
            ValidateCsv(Path.Combine(projectRoot, "docs/preproduction/STAGE_PLAN.csv"), "stage_id,chapter,branch,enemies");
            ValidateCsv(Path.Combine(projectRoot, "docs/preproduction/ECONOMY.csv"), "currency_id,type,obtain_method,usage");
            Debug.Log("AI Unbound Phase 2 data validation passed.");
        }

        private static void ValidateCsv(string path, string requiredHeader)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Required preproduction data is missing", path);
            using (var reader = new StreamReader(path))
            {
                var header = reader.ReadLine();
                if (header == null || !header.StartsWith(requiredHeader, StringComparison.Ordinal))
                    throw new InvalidDataException("Unexpected CSV header: " + path);
                var rowCount = 0;
                while (reader.ReadLine() != null) rowCount++;
                if (rowCount == 0) throw new InvalidDataException("CSV has no data rows: " + path);
            }
        }
    }
}
