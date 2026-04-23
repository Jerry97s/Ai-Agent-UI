namespace AiAgentUi.Services;

public interface IFileDialogService
{
    bool TryPickTextFile(out string filePath);
}

