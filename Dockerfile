FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json workspace.slnx ./
COPY src/McpServer/McpServer.csproj src/McpServer/
COPY src/AgentClient/AgentClient.csproj src/AgentClient/
RUN dotnet restore workspace.slnx

COPY src/ ./src/
RUN dotnet publish src/McpServer/McpServer.csproj -c Release -o /out/mcp-server --no-restore
RUN dotnet publish src/AgentClient/AgentClient.csproj -c Release -o /out/agent-client --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /out/agent-client/ ./
COPY --from=build /out/mcp-server/ /app/mcp-server/

ENV MCP_SERVER_DLL=/app/mcp-server/McpServer.dll
ENTRYPOINT ["dotnet", "AgentClient.dll"]
