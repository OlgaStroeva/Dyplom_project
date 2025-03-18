FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Dyplom_project/Dyplom_project.csproj", "Dyplom_project/"]
RUN dotnet restore "Dyplom_project/Dyplom_project.csproj"
COPY . .
WORKDIR "/src/Dyplom_project"
RUN dotnet build "Dyplom_project.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Dyplom_project.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Dyplom_project.dll"]
