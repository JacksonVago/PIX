# Usa a imagem oficial de runtime do ASP.NET (substitua a versão 8.0 se necessário pela sua)
#FROM mcr.microsoft.com/dotnet/aspnet:8.0
FROM mcr.microsoft.com/dotnet/core/sdk:2.2

# Define o diretório de trabalho de dentro do container
WORKDIR /app

# Copia o conteúdo da sua pasta de arquivos compilados para dentro do container
COPY . .

# Expõe a porta padrão que a API irá escutar
EXPOSE 8080

# Define o comando de inicialização apontando para a DLL principal da sua API
ENTRYPOINT ["dotnet", "PIX.dll"]