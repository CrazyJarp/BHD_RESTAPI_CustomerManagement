FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /BHDApi

COPY . ./
RUN dotnet restore

RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /BHDApi
COPY --from=build /app/out .

EXPOSE 80
EXPOSE 8080

ENTRYPOINT ["dotnet", "BHDApi.dll"]
