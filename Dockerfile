# Stage 1: Build the application
FROM ://mcr.microsoft.com AS build
WORKDIR /src
COPY ["scale-api-poc.csproj", "."]
RUN dotnet restore
COPY . .
WORKDIR /src
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Create the runtime image
FROM ://mcr.microsoft.com AS final
WORKDIR /app
COPY --from=build /app/publish .
# Cloud Run requires the application to listen on port 8080 by default
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Scale.Api.Poc.dll"]