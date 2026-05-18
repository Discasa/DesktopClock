using System.Globalization;

namespace DesktopClock.SetupUi;

internal static class SetupStrings
{
    private static readonly Dictionary<string, Dictionary<string, string>> Values = new()
    {
        ["en"] = new Dictionary<string, string>
        {
            ["InstallerTitleBar"] = "Desktop Clock Installer",
            ["UninstallerTitleBar"] = "Desktop Clock Uninstaller",
            ["InstallTitle"] = "Install Desktop Clock",
            ["InstallBody"] = "This setup will install Desktop Clock and configure startup, Start Menu shortcuts, and Windows Installed Apps integration.",
            ["InstallLocation"] = "Install location",
            ["ReadyToInstall"] = "Ready to install.",
            ["InstallButton"] = "Install",
            ["Cancel"] = "Cancel",
            ["InstallingTitle"] = "Installing Desktop Clock",
            ["InstallingBody"] = "Please wait while setup installs the application.",
            ["InstalledTitle"] = "Desktop Clock installed",
            ["InstalledBody"] = "The clock is installed and ready to use.",
            ["InstalledStatus"] = "Desktop Clock was installed successfully.",
            ["InstallFailedTitle"] = "Installation failed",
            ["SetupCouldNotComplete"] = "Setup could not complete.",
            ["Finish"] = "Finish",
            ["Close"] = "Close",
            ["PreparingInstall"] = "Preparing installation...",
            ["RemovingStartup"] = "Removing old startup entries...",
            ["CreatingFolder"] = "Creating installation folder...",
            ["CopyingFiles"] = "Copying application files...",
            ["AppNotCopied"] = "Application executable was not copied.",
            ["CreatingShortcuts"] = "Creating Start Menu shortcuts...",
            ["RegisteringInstalledApps"] = "Registering Windows Installed Apps entry...",
            ["ConfiguringStartup"] = "Configuring startup entry...",
            ["SkippingStartup"] = "Skipping startup entry for this test run...",
            ["StartingApp"] = "Starting Desktop Clock...",
            ["UninstallTitle"] = "Uninstall Desktop Clock",
            ["UninstallBody"] = "This will remove Desktop Clock, startup entries, Start Menu shortcuts, Windows Installed Apps integration, and installed files.",
            ["InstalledLocation"] = "Installed location",
            ["ReadyToUninstall"] = "Ready to uninstall.",
            ["UninstallButton"] = "Uninstall",
            ["UninstallingTitle"] = "Uninstalling Desktop Clock",
            ["UninstallingBody"] = "Please wait while setup removes the application.",
            ["UninstalledTitle"] = "Desktop Clock uninstalled",
            ["AlreadyUninstalledTitle"] = "Desktop Clock already uninstalled",
            ["UninstalledStatus"] = "Desktop Clock was uninstalled successfully.",
            ["AlreadyUninstalledStatus"] = "Desktop Clock was already uninstalled. Remaining entries were cleaned up.",
            ["UninstallFailedTitle"] = "Uninstall failed",
            ["StoppingApp"] = "Stopping Desktop Clock...",
            ["RemovingShortcuts"] = "Removing Start Menu shortcuts...",
            ["RemovingRegistry"] = "Removing Windows Installed Apps entry...",
            ["SchedulingRemoval"] = "Scheduling application file removal...",
            ["FinishingCleanup"] = "Finishing cleanup..."
        },
        ["pt-BR"] = new Dictionary<string, string>
        {
            ["InstallerTitleBar"] = "Instalador do Desktop Clock",
            ["UninstallerTitleBar"] = "Desinstalador do Desktop Clock",
            ["InstallTitle"] = "Instalar Desktop Clock",
            ["InstallBody"] = "Este instalador vai instalar o Desktop Clock e configurar inicializacao, atalhos do Menu Iniciar e integracao com Aplicativos instalados do Windows.",
            ["InstallLocation"] = "Local de instalacao",
            ["ReadyToInstall"] = "Pronto para instalar.",
            ["InstallButton"] = "Instalar",
            ["Cancel"] = "Cancelar",
            ["InstallingTitle"] = "Instalando Desktop Clock",
            ["InstallingBody"] = "Aguarde enquanto o instalador instala o aplicativo.",
            ["InstalledTitle"] = "Desktop Clock instalado",
            ["InstalledBody"] = "O relogio foi instalado e esta pronto para uso.",
            ["InstalledStatus"] = "O Desktop Clock foi instalado com sucesso.",
            ["InstallFailedTitle"] = "Falha na instalacao",
            ["SetupCouldNotComplete"] = "A instalacao nao pode ser concluida.",
            ["Finish"] = "Finalizar",
            ["Close"] = "Fechar",
            ["PreparingInstall"] = "Preparando a instalacao...",
            ["RemovingStartup"] = "Removendo entradas antigas de inicializacao...",
            ["CreatingFolder"] = "Criando pasta de instalacao...",
            ["CopyingFiles"] = "Copiando arquivos do aplicativo...",
            ["AppNotCopied"] = "O executavel do aplicativo nao foi copiado.",
            ["CreatingShortcuts"] = "Criando atalhos no Menu Iniciar...",
            ["RegisteringInstalledApps"] = "Registrando entrada em Aplicativos instalados do Windows...",
            ["ConfiguringStartup"] = "Configurando inicializacao automatica...",
            ["SkippingStartup"] = "Ignorando inicializacao automatica neste teste...",
            ["StartingApp"] = "Iniciando Desktop Clock...",
            ["UninstallTitle"] = "Desinstalar Desktop Clock",
            ["UninstallBody"] = "Isto vai remover o Desktop Clock, entradas de inicializacao, atalhos do Menu Iniciar, integracao com Aplicativos instalados do Windows e arquivos instalados.",
            ["InstalledLocation"] = "Local instalado",
            ["ReadyToUninstall"] = "Pronto para desinstalar.",
            ["UninstallButton"] = "Desinstalar",
            ["UninstallingTitle"] = "Desinstalando Desktop Clock",
            ["UninstallingBody"] = "Aguarde enquanto o desinstalador remove o aplicativo.",
            ["UninstalledTitle"] = "Desktop Clock desinstalado",
            ["AlreadyUninstalledTitle"] = "Desktop Clock ja estava desinstalado",
            ["UninstalledStatus"] = "O Desktop Clock foi desinstalado com sucesso.",
            ["AlreadyUninstalledStatus"] = "O Desktop Clock ja estava desinstalado. As entradas restantes foram limpas.",
            ["UninstallFailedTitle"] = "Falha na desinstalacao",
            ["StoppingApp"] = "Encerrando Desktop Clock...",
            ["RemovingShortcuts"] = "Removendo atalhos do Menu Iniciar...",
            ["RemovingRegistry"] = "Removendo entrada em Aplicativos instalados do Windows...",
            ["SchedulingRemoval"] = "Agendando remocao dos arquivos do aplicativo...",
            ["FinishingCleanup"] = "Finalizando limpeza..."
        }
    };

    public static string DetectLanguage()
    {
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("pt", StringComparison.OrdinalIgnoreCase)
            ? "pt-BR"
            : "en";
    }

    public static string Get(string language, string key)
    {
        if (!Values.TryGetValue(language, out var languageValues))
        {
            languageValues = Values["en"];
        }

        return languageValues.TryGetValue(key, out var value) ? value : Values["en"][key];
    }
}
