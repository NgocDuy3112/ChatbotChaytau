FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY chatbotchaytau.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish chatbotchaytau.csproj -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=true

COPY --from=build /out .

RUN mkdir -p /app/App_Data/documents && chown -R $APP_UID:$APP_UID /app/App_Data

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "chatbotchaytau.dll"]
