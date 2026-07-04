FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["AriyAI.ERP.Api.csproj", "./"]
RUN dotnet restore "./AriyAI.ERP.Api.csproj"

# Copy everything else and build the app
COPY . .
RUN dotnet publish "./AriyAI.ERP.Api.csproj" -c Release -o /app/publish

# Use the runtime image to run the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose port 8080 (standard for cloud services)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AriyAI.ERP.Api.dll"]
