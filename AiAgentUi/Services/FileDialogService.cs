namespace AiAgentUi.Services;

public sealed class FileDialogService : IFileDialogService
{
    public bool TryPickTextFile(out string filePath)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "업로드할 로그/텍스트 파일 선택",
            Filter = "텍스트/로그 파일|*.txt;*.log;*.csv;*.json;*.xml;*.yml;*.yaml;*.md|모든 파일|*.*",
            Multiselect = false,
            CheckFileExists = true,
        };

        var ok = dlg.ShowDialog() == true;
        filePath = ok ? dlg.FileName : "";
        return ok;
    }
}

