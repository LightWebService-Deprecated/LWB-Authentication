FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["LWS-Authentication/LWS-Authentication.csproj", "LWS-Authentication/"]
RUN dotnet restore "LWS-Authentication/LWS-Authentication.csproj"
COPY . .
WORKDIR "/src/LWS-Authentication"
RUN dotnet build "LWS-Authentication.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LWS-Authentication.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LWS-Authentication.dll"]
