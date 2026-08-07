FROM mcr.microsoft.com/dotnet/sdk:11.0-preview AS build
WORKDIR /src
COPY CodeFormat/CodeFormat.csproj CodeFormat/
RUN dotnet restore CodeFormat/CodeFormat.csproj
COPY . .
RUN dotnet publish CodeFormat/CodeFormat.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:11.0-preview
COPY --from=build /app /app
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
ENTRYPOINT [ "/entrypoint.sh" ]