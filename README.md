# ASP.NET Core Registration BFF / BFF de Cadastro ASP.NET Core

Multilingual README: English first, Português logo abaixo.

---

## English

### Overview

This project is a minimal ASP.NET Core 9.0 backend-for-frontend (BFF) designed to receive registration data from the companion React/Vite frontend. It validates the payload, persists records into a MySQL database, and exposes an OpenAPI/Swagger endpoint for manual testing.

### Features

- Minimal API setup with `MapGet`/`MapPost` endpoints.
- `/api/registrations` `POST` inserts a new person (name, birth date, CPF, email) and returns the stored entity.
- `/api/registrations` `GET` lists registrations ordered by creation date.
- Basic validation (CPF must have 11 digits, email format check, required fields).
- MySQL persistence using `MySqlConnector`, with automatic table creation on first run.
- Swagger UI available in development for quick inspection/testing.
- CORS policy restricted to the React dev server (`http://localhost:5173` and `https://localhost:5173`).

### Requirements

- .NET SDK 9.x
- MySQL server (local or remote)
- React frontend (optional) located at `../reactvite-app`

### Configuration

1. Duplicate the connection string in either `appsettings.Development.json` or `appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost;Port=3306;Database=reactvite_app;User Id=app_user;Password=your_password;SslMode=None;"
   }
   ```
   Adjust `User Id`, `Password`, `Database`, or `Server` to match your environment.

2. Ensure the MySQL schema exists:
   ```sql
   CREATE DATABASE IF NOT EXISTS reactvite_app;
   ```

3. Optional: trust the HTTPS development certificate (required for HTTPS profile):
   ```powershell
   dotnet dev-certs https --clean
   dotnet dev-certs https --trust
   ```

### Running

```powershell
dotnet restore
dotnet run --launch-profile https
```

The HTTPS profile matches the frontend expectation (`https://localhost:7242`). If you prefer plain HTTP, remove `UseHttpsRedirection` in `Program.cs`, change `VITE_API_BASE_URL` in the React app, and run with `--launch-profile http`.

Once running, open `https://localhost:7242/swagger` to inspect the OpenAPI UI. Keep this terminal session alive while the frontend sends requests.

### Testing the Endpoints

- **POST `/api/registrations`**
  ```bash
  curl -k -X POST https://localhost:7242/api/registrations \
    -H "Content-Type: application/json" \
    -d '{
      "name": "Ana Silva",
      "birthDate": "1990-05-10",
      "cpf": "12345678901",
      "email": "ana@example.com"
    }'
  ```
- **GET `/api/registrations`**
  ```bash
  curl -k https://localhost:7242/api/registrations
  ```

Swagger provides the same interactions with a UI.

### Project Structure (excerpt)

```
Contracts/
  RegistrationRequest.cs   # DTO for incoming payloads
  RegistrationResponse.cs  # DTO for responses
Models/
  PersonRegistration.cs    # Database model
Services/
  RegistrationService.cs   # Validation + persistence logic
Program.cs                 # Minimal API setup
Properties/launchSettings.json
appsettings*.json
```

### Integration with React Frontend

- The React app reads the base URL from `.env (VITE_API_BASE_URL)`, defaulting to `https://localhost:7242`.
- CORS policy allows `http://localhost:5173` and `https://localhost:5173`; add more origins under `Frontend:AllowedOrigins` if needed.
- After altering `.env` or the CORS list, restart the corresponding server (`npm run dev` or `dotnet run`).

### Known Limitations & Next Steps

- No authentication/authorization—add middleware if required.
- Error handling is minimal; consider adding centralized exception handling and logging.
- CPF validation only checks length; introduce full validation rules if needed.
- Replace inline table creation with migrations (e.g., Entity Framework Core) for large-scale projects.

---

## Português

### Visão Geral

Este projeto é um backend-for-frontend (BFF) ASP.NET Core 9.0 minimalista. Ele recebe dados de cadastro vindos do frontend React/Vite, valida o payload, grava os registros em um banco MySQL e expõe o Swagger para testes manuais.

### Funcionalidades

- Minimal API com endpoints `MapGet` e `MapPost`.
- `/api/registrations` `POST` insere uma nova pessoa (nome, nascimento, CPF, e-mail) e retorna a entidade armazenada.
- `/api/registrations` `GET` lista os cadastros em ordem decrescente de criação.
- Validações básicas (CPF com 11 dígitos, formato de e-mail, campos obrigatórios).
- Persistência em MySQL usando `MySqlConnector`, com criação automática da tabela na primeira execução.
- Swagger UI disponível em desenvolvimento.
- Política de CORS restrita ao servidor de desenvolvimento React (`http://localhost:5173` e `https://localhost:5173`).

### Requisitos

- .NET SDK 9.x
- Servidor MySQL
- Frontend React (opcional) localizado em `../reactvite-app`

### Configuração

1. Ajuste a string de conexão em `appsettings.Development.json` ou `appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost;Port=3306;Database=reactvite_app;User Id=app_user;Password=sua_senha;SslMode=None;"
   }
   ```

2. Crie o banco se ainda não existir:
   ```sql
   CREATE DATABASE IF NOT EXISTS reactvite_app;
   ```

3. (Opcional) Confie no certificado HTTPS de desenvolvimento:
   ```powershell
   dotnet dev-certs https --clean
   dotnet dev-certs https --trust
   ```

### Execução

```powershell
dotnet restore
dotnet run --launch-profile https
```

O perfil HTTPS corresponde ao que o frontend espera (`https://localhost:7242`). Se preferir rodar apenas em HTTP, remova `UseHttpsRedirection` do `Program.cs`, ajuste o `.env` do React e rode com `--launch-profile http`.

Com a aplicação rodando, acesse `https://localhost:7242/swagger` e mantenha o processo ativo enquanto realizar testes ou integrar com o frontend.

### Testando os Endpoints

- **POST `/api/registrations`**
  ```bash
  curl -k -X POST https://localhost:7242/api/registrations \
    -H "Content-Type: application/json" \
    -d '{
      "name": "Ana Silva",
      "birthDate": "1990-05-10",
      "cpf": "12345678901",
      "email": "ana@example.com"
    }'
  ```
- **GET `/api/registrations`**
  ```bash
  curl -k https://localhost:7242/api/registrations
  ```

O Swagger oferece a mesma possibilidade com interface gráfica.

### Estrutura do Projeto (trecho)

```
Contracts/
  RegistrationRequest.cs   # DTO de entrada
  RegistrationResponse.cs  # DTO de saída
Models/
  PersonRegistration.cs    # Modelo persistido
Services/
  RegistrationService.cs   # Regra de validação e persistência
Program.cs                 # Configuração da Minimal API
Properties/launchSettings.json
appsettings*.json
```

### Integração com o Frontend React

- O app React lê a base da API em `.env (VITE_API_BASE_URL)`, por padrão `https://localhost:7242`.
- A política de CORS libera `http://localhost:5173` e `https://localhost:5173`; adicione novos domínios em `Frontend:AllowedOrigins` conforme necessário.
- Após alterar `.env` ou o CORS, reinicie o servidor correspondente (`npm run dev` ou `dotnet run`).

### Limitações e Próximos Passos

- Não há autenticação/autorização; adicione middleware conforme necessário.
- Tratamento de erros é simples—considere centralizar exceções e logs estruturados.
- A validação do CPF considera somente comprimento; implemente regras adicionais se o negócio requerer.
- Para evoluções maiores, substitua a criação de tabela inline por migrations (Ex.: Entity Framework Core).
