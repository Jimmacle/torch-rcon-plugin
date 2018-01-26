[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.VisualBasic')


if (!(Test-Path TorchInstall\Torch.Server.exe))
{
    $title = 'Create game binary symlink'
    $msg   = 'Enter the full path of the directory containing Torch.Server.exe:'
    $text = [Microsoft.VisualBasic.Interaction]::InputBox($msg, $title)

    if (!(Test-Path $text\Torch.Server.exe))
    {
        Write-Error "Error: given directory does not contain Torch.Server.exe."
        return
    }

    cmd /c mklink /J TorchInstall "$text"
    echo "Symlink created to $text"
}
else
{
    echo "Symlink validated."
}