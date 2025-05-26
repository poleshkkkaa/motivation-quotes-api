# Базовий образ з .NET SDK 6
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

WORKDIR /app

# Копіюємо csproj і відновлюємо залежності
COPY *.csproj ./
RUN dotnet restore

# Копіюємо все решту і збираємо
COPY . ./
RUN dotnet publish -c Release -o /out

# Створюємо runtime-only образ
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build /out .

ENTRYPOINT ["dotnet", "MotivationQuotesApi.dll"]
