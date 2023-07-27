FROM mcr.microsoft.com/dotnet/aspnet:7.0 as base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 as publish
WORKDIR /src
COPY Backend.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /publish

FROM base as runtime
WORKDIR /app
COPY --from=publish /publish .
ENTRYPOINT [ "dotnet", "Backend.dll" ]