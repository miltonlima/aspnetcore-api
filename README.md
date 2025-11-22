# ASP.NET Core API - User Management & Registration BFF
# API ASP.NET Core - BFF de Cadastro e Gerenciamento de Usuários

Multilingual README: English first, Português logo abaixo.

---

## English

### Overview

This project is an ASP.NET Core 9.0 backend that has evolved from a simple registration backend-for-frontend (BFF) into a more complete service with full user management capabilities.

It provides a secure RESTful API for user registration, login (via JWT), and management. It persists records into a MySQL database and can serve a companion Single-Page Application (SPA). It includes an OpenAPI/Swagger endpoint for easy exploration and testing.

### Features

- **Authentication & Authorization**: Secure endpoints using JWT Bearer tokens.
- **User Management**:
    - `POST /api/login`: Authenticates a user and returns a JWT.
    - `POST /api/registrations`: Registers a new user, securely hashing the password with **BCrypt**.
- **Full CRUD for Registrations**:
    - `GET /api/registrations`: Lists all registered users (authorized access only).
    - `PUT /api/registrations/{id}`: Updates a user's information (authorized access only).
    - `DELETE /api/registrations/{id}`: Deletes a user (authorized access only).
- **Database**:
    - Persistence with MySQL using a mix of ADO.NET (`MySqlConnector`) and Dapper.
    - Automatic table creation and lightweight schema migration (e.g., adds new columns on startup).
- **Validation**:
    - Robust validation for required fields, email format, CPF (11 digits), password (min. 8 characters), and description length.
- **Development Experience**:
    - Swagger UI for API testing.
    - CORS policy easily configurable for frontend development servers.
    - Can host and serve the frontend's static build files.
- **Sample Endpoints**: Includes `/api/dashboard` and `/api/profile` as examples of protected resources.

### Requirements

- .NET SDK 9.x
- A running MySQL server instance.

### Configuration

1.  **Database Connection**: Set your `DefaultConnection` string in `appsettings.Development.json` or `appsettings.json`:
    ```json
    "ConnectionStrings": {
      "DefaultConnection": "Server=localhost;Port=3306;Database=aspnetcore_api;User Id=your_user;Password=your_password;SslMode=None;"
    }
    ```
    The database (`aspnetcore_api` in the example) will be created if it doesn't exist.

2.  **JWT Authentication**: Add a `Jwt` section to `appsettings.json` for token generation.
    ```json
    "Jwt": {
      "Key": "A_SECRET_KEY_THAT_IS_LONG_AND_SECURE_ENOUGH",
      "Issuer": "https://localhost:7242",
      "Audience": "https://localhost:7242"
    }
    ```
    **Important**: The `Jwt:Key` must be a strong, secret value.

3.  **CORS Origins**: The API allows requests from `http://localhost:5173` by default. To add other origins, modify `appsettings.json`:
    ```json
    "Frontend": {
      "AllowedOrigins": [
        "http://localhost:5173",
        "https://localhost:5173",
        "http://your-other-domain.com"
      ]
    }
    ```

4.  **HTTPS Certificate (Optional)**: If running locally over HTTPS for the first time, trust the development certificate:
    ```powershell
    dotnet dev-certs https --trust
    ```

### Running the Application

```powershell
dotnet restore
dotnet run --launch-profile https
```
Once running, the API is available at `https://localhost:7242`. The Swagger UI can be accessed at `https://localhost:7242/swagger`.

### API Endpoints

| Method | Path                        | Description                                     | Auth? |
|--------|-----------------------------|-------------------------------------------------|-------|
| `POST` | `/api/login`                | Authenticates a user and returns a JWT.         | No    |
| `POST` | `/api/registrations`        | Creates a new user registration.                | No    |
| `GET`    | `/api/registrations`        | Retrieves a list of all users.                  | Yes   |
| `PUT`    | `/api/registrations/{id}`   | Updates an existing user's details.             | Yes   |
| `DELETE` | `/api/registrations/{id}`   | Deletes a user.                                 | Yes   |
| `GET`    | `/api/profile`              | Example: Returns a static user profile.         | Yes   |
| `GET`    | `/api/dashboard`            | Example: Returns a static dashboard message.    | Yes   |

#### Example `curl` Commands

-   **Register a new user:**
    ```bash
    curl -X POST https://localhost:7242/api/registrations -k \
      -H "Content-Type: application/json" \
      -d 
      {
        "name": "Ana Silva", "birthDate": "1990-05-10", "cpf": "12345678901",
        "email": "ana@example.com", "password": "SecurePassword123"
      }
    ```

-   **Log in:**
    ```bash
    curl -X POST https://localhost:7242/api/login -k \
      -H "Content-Type: application/json" \
      -d '{"username": "ana@example.com", "password": "SecurePassword123"}'
    ```
    *This will return a `token` to be used in subsequent requests.*

-   **Access a protected resource (e.g., list users):**
    ```bash
    TOKEN="your_jwt_token_here"
    curl -X GET https://localhost:7242/api/registrations -k \
      -H "Authorization: Bearer $TOKEN"
    ```

### Known Issues & Next Steps

-   **Unused Code**: The project contains `UserService.cs`, which implements a separate and insecure (unsalted SHA256 hashing) user system. This service is not used by the application but should be removed to avoid confusion and security risks.
-   **Error Handling**: Could be improved with a global exception handling middleware.
-   **CPF Validation**: Only checks for digit count. A full algorithm-based validation could be implemented.

---

## Português

### Visão Geral

Este projeto é um backend ASP.NET Core 9.0 que evoluiu de um simples BFF (Backend-for-Frontend) de cadastro para um serviço mais completo, com funcionalidades de gerenciamento de usuários.

Ele provê uma API REST segura para cadastro de usuários, login (via JWT) e gerenciamento. Os registros são persistidos em um banco de dados MySQL, e o backend pode servir uma aplicação SPA (Single-Page Application). Inclui também um endpoint OpenAPI/Swagger para facilitar a exploração e os testes.

### Funcionalidades

- **Autenticação e Autorização**: Endpoints protegidos usando tokens JWT Bearer.
- **Gerenciamento de Usuários**:
    - `POST /api/login`: Autentica um usuário e retorna um JWT.
    - `POST /api/registrations`: Cadastra um novo usuário, usando **BCrypt** para armazenar a senha de forma segura.
- **CRUD Completo de Cadastros**:
    - `GET /api/registrations`: Lista todos os usuários cadastrados (acesso autorizado).
    - `PUT /api/registrations/{id}`: Atualiza as informações de um usuário (acesso autorizado).
    - `DELETE /api/registrations/{id}`: Deleta um usuário (acesso autorizado).
- **Gestão de Unidades de Ensino**:
  - `GET /api/education-units`: Lista unidades educacionais cadastradas.
  - `POST /api/education-units`: Cria uma nova unidade.
  - `PUT /api/education-units/{id}` e `DELETE /api/education-units/{id}`: Atualizam ou removem unidades existentes.
- **Banco de Dados**:
    - Persistência em MySQL usando uma mistura de ADO.NET (`MySqlConnector`) e Dapper.
    - Criação automática de tabelas e migração de schema leve (ex: adiciona colunas ao iniciar a aplicação).
- **Validação**:
  - Validação robusta para campos obrigatórios, formato de e-mail, CPF (11 dígitos), senha obrigatória e tamanho da descrição.
- **Experiência de Desenvolvimento**:
    - Swagger UI para testes da API.
    - Política de CORS facilmente configurável para servidores de desenvolvimento frontend.
    - Capacidade de hospedar e servir os arquivos estáticos da build do frontend.
- **Endpoints de Exemplo**: Inclui `/api/dashboard` e `/api/profile` como exemplos de recursos protegidos.

### Requisitos

- .NET SDK 9.x
- Uma instância de servidor MySQL em execução.

### Configuração

1.  **Conexão com o Banco**: Defina sua string de conexão `DefaultConnection` em `appsettings.Development.json` ou `appsettings.json`:
    ```json
    "ConnectionStrings": {
      "DefaultConnection": "Server=localhost;Port=3306;Database=aspnetcore_api;User Id=seu_usuario;Password=sua_senha;SslMode=None;"
    }
    ```
    O banco de dados (`aspnetcore_api` no exemplo) será criado se não existir.

2.  **Autenticação JWT**: Adicione uma seção `Jwt` no `appsettings.json` para a geração dos tokens.
    ```json
    "Jwt": {
      "Key": "UMA_CHAVE_SECRETA_QUE_SEJA_LONGA_E_SEGURA_O_SUFICIENTE",
      "Issuer": "https://localhost:7242",
      "Audience": "https://localhost:7242"
    }
    ```
    **Importante**: A `Jwt:Key` deve ser um valor forte e secreto.

3.  **Origens CORS**: A API permite requisições de `http://localhost:5173` por padrão. Para adicionar outras origens, modifique o `appsettings.json`:
    ```json
    "Frontend": {
      "AllowedOrigins": [
        "http://localhost:5173",
        "https://localhost:5173",
        "http://seu-outro-dominio.com"
      ]
    }
    ```

4.  **Certificado HTTPS (Opcional)**: Se for executar localmente em HTTPS pela primeira vez, confie no certificado de desenvolvimento:
    ```powershell
    dotnet dev-certs https --trust
    ```

### Executando a Aplicação

```powershell
dotnet restore
dotnet run --launch-profile https
```
Após a execução, a API estará disponível em `https://localhost:7242`. A UI do Swagger pode ser acessada em `https://localhost:7242/swagger`.

### Endpoints da API

| Método | Caminho                     | Descrição                                       | Auth? |
|--------|-----------------------------|-------------------------------------------------|-------|
| `POST` | `/api/login`                | Autentica um usuário e retorna um JWT.          | Não   |
| `POST` | `/api/registrations`        | Cria um novo cadastro de usuário.               | Não   |
| `GET`    | `/api/registrations`        | Recupera a lista com todos os usuários.         | Sim   |
| `PUT`    | `/api/registrations/{id}`   | Atualiza os detalhes de um usuário existente.   | Sim   |
| `DELETE` | `/api/registrations/{id}`   | Deleta um usuário.                              | Sim   |
| `GET`    | `/api/profile`              | Exemplo: Retorna um perfil de usuário estático. | Sim   |
| `GET`    | `/api/dashboard`            | Exemplo: Retorna uma mensagem de painel estática. | Sim   |

#### Exemplos de Comandos `curl`

-   **Cadastrar um novo usuário:**
    ```bash
    curl -X POST https://localhost:7242/api/registrations -k \
      -H "Content-Type: application/json" \
      -d 
      {
        "name": "Ana Silva", "birthDate": "1990-05-10", "cpf": "12345678901",
        "email": "ana@example.com", "password": "SenhaForte123"
      }
    ```

-   **Fazer login:**
    ```bash
    curl -X POST https://localhost:7242/api/login -k \
      -H "Content-Type: application/json" \
      -d '{"username": "ana@example.com", "password": "SenhaForte123"}'
    ```
    *Isso retornará um `token` para ser usado nas próximas requisições.*

-   **Acessar um recurso protegido (ex: listar usuários):**
    ```bash
    TOKEN="seu_token_jwt_aqui"
    curl -X GET https://localhost:7242/api/registrations -k \
      -H "Authorization: Bearer $TOKEN"
    ```

### Problemas Conhecidos & Próximos Passos

-   **Código Não Utilizado**: O projeto contém o arquivo `UserService.cs`, que implementa um sistema de usuários separado e inseguro (usa hash SHA256 sem salt). Este serviço não é utilizado pela aplicação, mas deve ser removido para evitar confusão e riscos de segurança.
-   **Tratamento de Erros**: Pode ser melhorado com um middleware de tratamento de exceções global.
-   **Validação de CPF**: Atualmente verifica apenas o número de dígitos. Uma validação completa baseada no algoritmo poderia ser implementada.