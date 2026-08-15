FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY CodeFormat.Cli/CodeFormat.Cli.csproj CodeFormat.Cli/
COPY CodeFormat.Rules/CodeFormat.Rules.csproj CodeFormat.Rules/
RUN dotnet restore CodeFormat.Cli/CodeFormat.Cli.csproj
COPY . .
RUN dotnet publish CodeFormat.Cli/CodeFormat.Cli.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
COPY --from=build /app /app
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
ENTRYPOINT [ "/entrypoint.sh" ]