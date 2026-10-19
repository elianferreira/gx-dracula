# GxDracula

Tema [Dracula](https://draculatheme.com) para a IDE do GeneXus 18: janelas, menus, toolbars, grids,
editores de código, Team Development, Variables, Patterns e a janela Options, com ícones Codicons
e a fonte Inter nos menus.

Não altera KB nem objetos: é um pacote da IDE que pinta os controles em tempo de execução.

## Instalação

Pré-requisitos: Windows, GeneXus 18, PowerShell 5.1+.

1. Feche a IDE do GeneXus.
2. Gere o instalador (ou use um já gerado):
   ```powershell
   .\build.ps1; .\pack.ps1; .\pack-single.ps1   # gera dist\GxDracula-Instalador.ps1
   ```
3. Rode na sessão do usuário que vai usar a IDE e aceite o UAC:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\GxDracula-Instalador.ps1
   ```

Parâmetros: `-GeneXusPath 'D:\GeneXus18'`, `-LiberarPreviewWWP` (corrige o preview em branco do
Patterns), `-DryRun`, `-Desinstalar`, `-ExtrairPara <pasta>`.

O instalador copia `GxDracula.dll` para `<GeneXus18>\Packages` e roda `GeneXus.exe /install`, copia o
tema para `%LOCALAPPDATA%\GxDracula` e instala a fonte Inter só para o usuário atual.

## Uso

- **Desligar o tema:** crie o arquivo vazio `%APPDATA%\GxDracula.disabled` e reabra a IDE.
- **Só os ícones:** `%APPDATA%\GxDracula.icons.disabled`.
- **Atualizar:** troque `GxDracula.Theme.dll` em `%LOCALAPPDATA%\GxDracula`; a IDE aberta recarrega em ~2 s.
- **Desinstalar:** IDE fechada, `-Desinstalar` no instalador (ou `installer\uninstall.ps1`).
- **Log:** `%TEMP%\GxDracula.log`.

## Desenvolvimento

- `src\` — C# 5 / .NET Framework, compilado por `build.ps1` (csc), referenciando as DLLs do GeneXus 18.
- `build.ps1` publica o tema em `%LOCALAPPDATA%\GxDracula`: com a IDE aberta, a mudança aparece sem reiniciar.
- Diagnóstico: crie `%TEMP%\GxDracula.dump` para gravar a árvore de controles em `%TEMP%\GxDracula.tree.txt`;
  comandos em `%TEMP%\GxDracula.cmd` (`doc`, `tab`, `node`, `shot`, `combo`, `probe`...).
- Detalhes em [MANUAL.md](MANUAL.md).

## Licenças

Paleta Dracula (MIT), ícones [Codicons](https://github.com/microsoft/vscode-codicons) (CC-BY-4.0),
fonte [Inter](https://rsms.me/inter/) (SIL OFL 1.1).
