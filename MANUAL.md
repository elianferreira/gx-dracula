# GxDracula - tema Dracula para a IDE do GeneXus 18

## O que é

Um package que pinta a IDE do GeneXus 18 com a paleta Dracula: docking, abas, editor de código, Properties,
Output, árvores, grades, barra de status, fontes (Cascadia Code no editor, Segoe UI na interface) e ícones da
toolbar (Codicons). São dois arquivos:

- `GxDracula.dll`: carregador, fica em `GeneXus18\Packages`.
- `GxDracula.Theme.dll`: o tema, fica em `%LOCALAPPDATA%\GxDracula` e é recarregado a quente.

Não altera a KB nem os objetos. Partes em HTML (designer do Web Form, Start Page, preview do WorkWithPlus)
continuam claras.

## Requisitos

- GeneXus 18 instalado.
- Windows (testado no Windows 11).
- .NET Framework 4.x (já vem com o Windows e com o GeneXus).
- Permissão de administrador (UAC) só durante a instalação.

## Instalação

1. Baixe `GxDracula.zip`.
2. Extraia o zip inteiro (não execute de dentro do zip).
3. **Feche a IDE do GeneXus** (salve o trabalho).
4. Na pasta `GxDracula`, clique com o botão direito em `install.ps1` > **Executar com PowerShell**.
   Ou, em um PowerShell:
   ```powershell
   powershell -ExecutionPolicy Bypass -File install.ps1
   ```
5. Aceite o aviso do UAC. O instalador copia os arquivos, roda `GeneXus.exe /install` e avisa "Instalado".
6. Abra o GeneXus 18.

Opções do `install.ps1`:

| Opção | Efeito |
|---|---|
| `-GeneXusPath 'D:\GeneXus18'` | Pasta do GeneXus, se não for `Program Files (x86)\GeneXus\GeneXus18` |
| `-LiberarPreviewWWP` | Dá permissão de escrita ao seu usuário na pasta de preview do WorkWithPlus (ver Solução de problemas) |
| `-DryRun` | Só mostra o que faria, sem alterar nada |

Se o instalador for elevado por outra conta de administrador, os arquivos do tema vão para o perfil de quem
abriu o instalador, não para o do administrador.

## Desligar

- Tema inteiro: crie um arquivo vazio `%APPDATA%\GxDracula.disabled` e reabra a IDE.
- Só os ícones da toolbar: crie `%APPDATA%\GxDracula.icons.disabled`.

Apague o arquivo para religar.

## Atualizar

Basta trocar `GxDracula.Theme.dll` em `%LOCALAPPDATA%\GxDracula`. A IDE aberta recarrega sozinha em cerca de
2 segundos. Só é preciso rodar o instalador de novo se a versão nova trouxer um `GxDracula.dll` diferente
(com a IDE fechada).

## Desinstalar

Com a IDE fechada, rode `uninstall.ps1` (mesmo método do `install.ps1`). Ele remove a DLL de `Packages`,
roda `GeneXus.exe /install` e pergunta se apaga `%LOCALAPPDATA%\GxDracula`. Use `-Force` para apagar sem
perguntar e `-DryRun` para só simular.

## Solução de problemas

- **Nada mudou na IDE:** veja o log em `%TEMP%\GxDracula.log`. Confira se não existe `%APPDATA%\GxDracula.disabled`
  e se `GxDracula.Theme.dll` está em `%LOCALAPPDATA%\GxDracula`.
- **Preview do WorkWithPlus em branco, "Access denied ... PadraoUnivaliResp.css":** não é o tema. O WorkWithPlus
  regrava arquivos em `Packages\Patterns\WorkWithPlus\PreviewImages`, dentro de Program Files, e a IDE sem
  administrador não tem permissão. Rode o instalador com `-LiberarPreviewWWP`.
- **IDE abre com UAC / como administrador:** o tema funciona igual, mas o `LOCALAPPDATA` é o do usuário que
  executa a IDE. Se a IDE roda com outra conta, copie `GxDracula.Theme.dll` e a pasta `codicons` para o
  `%LOCALAPPDATA%\GxDracula` dessa conta.
- **O instalador diz que a IDE está aberta:** feche todas as janelas do GeneXus (confira no Gerenciador de Tarefas).
- **GeneXus não encontrado:** informe `-GeneXusPath`.

## Créditos

- Paleta Dracula: Dracula Theme, licença MIT (https://draculatheme.com).
- Ícones: Codicons, Microsoft, licença CC-BY-4.0 (https://github.com/microsoft/vscode-codicons).

## Fonte dos menus (Inter)

O instalador também instala a fonte **Inter** (licença OFL, incluída em `fonts\OFL-Inter.txt`) só para o seu usuário, sem admin. Ela aparece nos menus e toolbars **depois de reabrir a IDE**. Para trocar a família, mude `MenuFont` em `src\Fonts.cs` e rode `build.ps1`.
