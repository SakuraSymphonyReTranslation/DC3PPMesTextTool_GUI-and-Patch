using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace DC3PPMesTextTool
{
    public class MesToolProcessor
    {
        private static readonly Encoding ShiftJIS;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        static MesToolProcessor()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ShiftJIS = Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        }

        public event Action<string>? Log;

        private void LogMessage(string message)
        {
            Log?.Invoke(message);
        }

        private void LogError(string message)
        {
            Log?.Invoke($"Error: {message}");
        }

        public int ExportMesToJson(string inputPath, string outputPath)
        {
            LogMessage($"Exporting {Path.GetFileName(inputPath)}... ");
            
            try
            {
                var script = new MesScript();
                script.Load(inputPath);
                
                var entries = script.ExtractText(ShiftJIS);
                
                if (entries.Count < 5)
                {
                    var patternEntries = script.ExtractTextPatternBased(ShiftJIS);
                    if (patternEntries.Count > entries.Count)
                        entries = patternEntries;
                }
                
                var processedEntries = ProcessEntries(entries);
                
                var outputList = new List<object>();
                foreach (var entry in processedEntries)
                {
                    if (!string.IsNullOrEmpty(entry.Name))
                    {
                        outputList.Add(new { name = entry.Name, message = entry.Message });
                    }
                    else
                    {
                        outputList.Add(new { message = entry.Message });
                    }
                }
                
                string json = JsonSerializer.Serialize(outputList, JsonOptions);
                File.WriteAllText(outputPath, json, Encoding.UTF8);
                
                LogMessage($"OK ({outputList.Count} entries)");
                return 0;
            }
            catch (Exception ex)
            {
                LogError($"Failed to export {inputPath}: {ex.Message}");
                return 1;
            }
        }

        private List<TextEntry> ProcessEntries(List<TextEntry> entries)
        {
            var processed = new List<TextEntry>();

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                // DC3PP stores a spoken line as two consecutive strings:
                // opcode 0x4F = nameplate, opcode 0x50 = dialogue text.
                // Pair by opcode; translated dialogue need not keep Japanese quotes.
                if (entry.Opcode == 0x4F &&
                    i + 1 < entries.Count &&
                    entries[i + 1].Opcode == 0x50)
                {
                    var dialogue = entries[++i];
                    processed.Add(new TextEntry
                    {
                        Offset = dialogue.Offset,
                        OriginalLength = dialogue.OriginalLength,
                        Opcode = dialogue.Opcode,
                        IsEncrypted = dialogue.IsEncrypted,
                        Name = entry.Message.Trim(),
                        Message = dialogue.Message,
                        IsChoice = dialogue.IsChoice
                    });
                    continue;
                }

                processed.Add(entry);
            }

            return processed;
        }

        private static bool IsDialogueMessage(string text)
        {
            string trimmed = text.TrimStart();
            return trimmed.StartsWith("「", StringComparison.Ordinal) ||
                   trimmed.StartsWith("『", StringComparison.Ordinal);
        }

        public int ImportJsonToMes(string jsonPath, string originalMesPath, string outputPath, int wrapWidth = TextFormatter.DefaultWidth)
        {
            LogMessage($"Importing {Path.GetFileName(jsonPath)}... ");
            
            try
            {
                string json = File.ReadAllText(jsonPath, Encoding.UTF8);
                var entries = JsonSerializer.Deserialize<List<TextEntry>>(json) 
                    ?? new List<TextEntry>();
                
                var expandedEntries = new List<TextEntry>();
                foreach (var entry in entries)
                {
                    if (!string.IsNullOrEmpty(entry.Name))
                    {
                        expandedEntries.Add(new TextEntry { Message = entry.Name });
                    }
                    expandedEntries.Add(new TextEntry { Message = entry.Message });
                }
                
                var script = new MesScript();
                script.Load(originalMesPath);
                
                TextFormatter.ValidateWidth(wrapWidth);
                var formatter = new TextFormatter { MaxLineLength = wrapWidth };
                var warnings = new List<LayoutWarning>();
                byte[] newMes = script.ImportText(expandedEntries, ShiftJIS, formatter, warnings.Add);
                File.WriteAllBytes(outputPath, newMes);
                // Sidecar uses a distinct extension so BatchImport never treats it as dialogue JSON.
                File.WriteAllText(outputPath + ".layout-report", JsonSerializer.Serialize(warnings, JsonOptions), Encoding.UTF8);
                if (warnings.Count > 0) LogMessage($"Layout: {warnings.Count} entries to review in {Path.GetFileName(outputPath)}.layout-report");
                
                LogMessage($"OK ({entries.Count} entries -> {expandedEntries.Count} expanded)");
                return 0;
            }
            catch (Exception ex)
            {
                LogError($"Failed to import {jsonPath}: {ex.Message}");
                return 1;
            }
        }

        public int BatchExport(string inputFolder, string outputFolder)
        {
            if (!Directory.Exists(inputFolder))
            {
                LogError($"Folder not found: {inputFolder}");
                return 1;
            }

            Directory.CreateDirectory(outputFolder);
            var mesFiles = Directory.GetFiles(inputFolder, "*.mes");
            LogMessage($"Found {mesFiles.Length} MES files in {inputFolder}");

            int success = 0;
            int failed = 0;

            foreach (var mesFile in mesFiles)
            {
                string outputPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(mesFile) + ".json");
                if (ExportMesToJson(mesFile, outputPath) == 0)
                    success++;
                else
                    failed++;
            }
            
            LogMessage($"Batch Export Completed: {success} success, {failed} failed");
            return failed > 0 ? 1 : 0;
        }

        public int BatchImport(string jsonFolder, string mesFolder, string outputFolder, int wrapWidth)
        {
            if (!Directory.Exists(jsonFolder))
            {
                LogError($"JSON folder not found: {jsonFolder}");
                return 1;
            }
            
            if (!Directory.Exists(mesFolder))
            {
                LogError($"MES folder not found: {mesFolder}");
                return 1;
            }

            Directory.CreateDirectory(outputFolder);
            var jsonFiles = Directory.GetFiles(jsonFolder, "*.json");
            LogMessage($"Found {jsonFiles.Length} JSON files (wrap width: {wrapWidth})");

            int success = 0;
            int failed = 0;

            foreach (var jsonFile in jsonFiles)
            {
                string baseName = Path.GetFileNameWithoutExtension(jsonFile);
                string mesFile = Path.Combine(mesFolder, baseName + ".mes");
                string outputPath = Path.Combine(outputFolder, baseName + ".mes");
                
                if (!File.Exists(mesFile))
                {
                    LogMessage($"Skipping {baseName} - original MES not found");
                    failed++;
                    continue;
                }
                
                if (ImportJsonToMes(jsonFile, mesFile, outputPath, wrapWidth) == 0)
                    success++;
                else
                    failed++;
            }

            LogMessage($"Batch Import Completed: {success} success, {failed} failed");
            return failed > 0 ? 1 : 0;
        }
    }
}
