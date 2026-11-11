## Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /src

## Copy project file and restore dependancies
COPY SecAudit.csproj ./
RUN dotnet restore SecAudit.csproj

##Copy source code and build application
COPY . ./
RUN dotnet build SecAudit.csproj -c Release -o /app/build

##Publish
FROM build AS publish
RUN dotnet publish SecAudit.csproj -c Release -o /app/publish /p:PublishReadyToRun=true

##Run stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
    
WORKDIR /src
EXPOSE 5000
ENV ASPNETCORE_HTTP_PORTS=5000

##Copy published app from build stage
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SecAudit.dll"]