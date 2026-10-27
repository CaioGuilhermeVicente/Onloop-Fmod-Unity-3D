using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class MacBuildZipper : IPostprocessBuildWithReport
{
    public int callbackOrder => 999;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneOSX)
            return;

        string appPath = report.summary.outputPath;
        if (!appPath.EndsWith(".app"))
        {
            Debug.LogWarning("O caminho de saída não é um pacote .app.");
            return;
        }

        string zipPath = appPath + ".zip";
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        Debug.Log("Empacotando .app com metadados nativos de Unix...");
        CreateUnixZip(appPath, zipPath);
        PatchZipHeadersToUnix(zipPath);
        Debug.Log($"Build pronta e corrigida para Mac: {zipPath}");
    }

    private void CreateUnixZip(string sourceAppDir, string zipFilePath)
    {
        string parentDir = Directory.GetParent(sourceAppDir).FullName;

        using (FileStream zipToOpen = new FileStream(zipFilePath, FileMode.Create))
        using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
        {
            // 1. Registra todas as pastas com permissão de execução/travessia (drwxr-xr-x / 040755)
            foreach (string dirPath in Directory.GetDirectories(sourceAppDir, "*", SearchOption.AllDirectories))
            {
                string relativeDir = Path.GetRelativePath(parentDir, dirPath).Replace('\\', '/') + "/";
                ZipArchiveEntry dirEntry = archive.CreateEntry(relativeDir);
                dirEntry.ExternalAttributes = (0x41ED << 16) | 0x10; // 040755 + flag de diretório DOS
            }

            // 2. Registra todos os arquivos e marca binários como executáveis (-rwxr-xr-x / 100755)
            foreach (string filePath in Directory.GetFiles(sourceAppDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(parentDir, filePath).Replace('\\', '/');
                ZipArchiveEntry entry = archive.CreateEntryFromFile(filePath, relativePath, System.IO.Compression.CompressionLevel.Optimal);

                // Pega o executável do jogo, o MonoBleedingEdge e os binários internos dos .bundle (FMOD)
                bool isExecutable = relativePath.Contains("/Contents/MacOS/") ||
                                    relativePath.Contains("/MonoBleedingEdge/bin/") ||
                                    relativePath.EndsWith(".dylib") ||
                                    relativePath.EndsWith(".so");

                int unixPermissions = isExecutable ? 0x81ED : 0x81A4; // 0x81ED = 755 (+x), 0x81A4 = 644
                entry.ExternalAttributes = (unixPermissions << 16);
            }
        }
    }

    // Força o cabeçalho binário do ZIP a declarar que foi criado no Unix (OS = 3)
    // Sem isso, o Archive Utility do macOS ignora o ExternalAttributes.
    private void PatchZipHeadersToUnix(string zipFilePath)
    {
        byte[] data = File.ReadAllBytes(zipFilePath);

        // Localiza o End of Central Directory (EOCD) de trás para frente (assinatura 0x06054b50)
        int eocdPos = -1;
        for (int i = data.Length - 22; i >= 0; i--)
        {
            if (data[i] == 0x50 && data[i + 1] == 0x4b && data[i + 2] == 0x05 && data[i + 3] == 0x06)
            {
                eocdPos = i;
                break;
            }
        }

        if (eocdPos == -1) return;

        int totalEntries = BitConverter.ToUInt16(data, eocdPos + 10);
        int cdOffset = BitConverter.ToInt32(data, eocdPos + 16);

        int pos = cdOffset;
        for (int i = 0; i < totalEntries; i++)
        {
            // Verifica assinatura de entrada do Central Directory (0x02014b50)
            if (data[pos] == 0x50 && data[pos + 1] == 0x4b && data[pos + 2] == 0x01 && data[pos + 3] == 0x02)
            {
                // Offset + 5 é o byte "Version Made By (Host OS)". Mudamos de 0 (FAT/Windows) para 3 (Unix)
                data[pos + 5] = 3;

                int fileNameLen = BitConverter.ToUInt16(data, pos + 28);
                int extraFieldLen = BitConverter.ToUInt16(data, pos + 30);
                int commentLen = BitConverter.ToUInt16(data, pos + 32);

                pos += 46 + fileNameLen + extraFieldLen + commentLen;
            }
            else
            {
                break;
            }
        }

        File.WriteAllBytes(zipFilePath, data);
    }
}