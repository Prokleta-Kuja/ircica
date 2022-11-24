FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /app

COPY ./src/ircica/*.csproj ./
RUN dotnet restore

COPY ./src/ircica .

ARG Version=0.0.0
RUN dotnet publish /p:Version=$Version -c Release -o out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./

ENV ASPNETCORE_URLS=http://*:50505 \
    LOCALE=en-US \
    TZ=America/Chicago

EXPOSE 50505

ENTRYPOINT ["dotnet", "ircica.dll"]