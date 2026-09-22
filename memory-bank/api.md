# API и контракты

Текущий внешний контракт — MCP через stdio, который предоставляет сервер
`src/McpServer`:

- `get_workspace_status` — возвращает статус сервера;
- `echo(text)` — возвращает переданный текст;
- `list_workspace_files(relativeDirectory)` — перечисляет разрешённые файлы;
- `read_workspace_file(relativePath)` — читает разрешённый текстовый файл;
- `replace_workspace_file(relativePath, content)` — заменяет содержимое файла;
- `apply_workspace_patch(patch)` — проверяет и применяет Git unified diff; для добавления новых файлов также принимает формат `*** Begin Patch` / `*** Add File`. Изменение существующего файла выполняется через unified diff или `replace_workspace_file`;
- `run_dotnet_check(command)` — запускает разрешённую команду `dotnet`;
- `get_workspace_diff()` — возвращает текущий diff workspace;
- `get_workspace_evidence()` — возвращает revision, hash diff и изменённые файлы.

Фиксируй здесь совместимые и несовместимые изменения MCP-инструментов и будущих
публичных контрактов.
