# API TbcaTest

API RESTful desenvolvida em .NET 8 para o gerenciamento de tarefas (CRUD com paginação), autenticação segura e importação em massa de planilhas Excel com alto desempenho.

---

##  Como Rodar a Aplicação

### 1. Pré-requisitos
* .NET 8 SDK instalado na máquina.
* SQL Server (ou instância LocalDB/Docker) acessível para testes de banco real.

### 2. Configuração do Banco de Dados
1. Configure a string de conexão `DefaultConnection` no arquivo appsettings.json.
2. Execute as migrations do Entity Framework Core para criar a estrutura do banco:
   ```bash
   dotnet ef database update --project TbcaTest.Infra --startup-project TbcaTest.Api
   ```
3. Para a Importação em Massa (Opcional - Performance SQL Server): Execute o script SQL localizado em TaskBulkInsert.sql em sua instância do SQL Server. Ele criará o tipo TVP (TaskImportType), o índice único no título e a procedure performática sp_InsertTaskBatch.

### 3. Execução da API
No terminal, na raiz do projeto ou dentro da pasta da API:
```bash
dotnet restore
dotnet run --project TbcaTest.Api
```
* A API estará acessível e poderá ser testada diretamente pelo Swagger UI.

### 4. Execução dos Testes (Unitários e de Integração)
O projeto conta com mais de 100 testes de domínio, serviços e testes de integração de endpoints reais.
```bash
dotnet test
```

---

## Decisões Arquiteturais e Boas Práticas

### 1. Separação de Responsabilidades (Clean Architecture & DDD)
Foi utilizado uma arquitetura de camadas com as seguintes camadas e suas características:
* TbcaTest.Domain: Camada central, livre de dependências externas. Contém as entidades (ex.: `TaskItem`, `Client`), value objects e enumerações.
* TbcaTest.Application: Contém os casos de uso do sistema, contratos das interfaces (`ITaskRepository`, `IUnitOfWork`), DTOs de entrada/saída e lógicas de orquestração de negócios nos serviços (`TaskService`).
* TbcaTest.Infra: Responsável pelo I/O e persistência. Implementa o Entity Framework Core para as operações CRUD/DDD padrão e o Dapper para as consultas e inserções de alta performance (Bulk Insert).
* TbcaTest.CrossCutting / TbcaTest.Api: Configurações transversais de IoC, middlewares de segurança, interceptores globais e os Controllers RESTful.

### 2. Injeção de Dependência (DI & IoC)
* Implementada de forma nativa e centralizada na camada de CrossCutting.
* O design orientado a interfaces permitiu o isolamento completo da lógica no momento dos testes unitários (via mocks com `Moq`).
* O padrão IOptions (`IOptions<AppSecurityOptions>`) é utilizado como Singleton para injetar configurações tipadas e limpas na aplicação.


### 4. Autenticação e Autorização Seguras
* Autenticação JWT Exclusiva & Segurança Reforçada: Segurança de endpoints centralizada e protegida unicamente via mecanismo nativo e escalável JWT Bearer Token (JSON Web Tokens), com proteção ativa contra enumeração por tempo (timing attacks) e bloqueio rigoroso de contas desativadas.
* Os tokens emitidos contêm apenas claims de identificação essenciais com mascaramento de dados (como e-mail), garantindo aderência a regulamentações de proteção de dados (LGPD).

### 5. Tratamento de Exceções Global
* O uso do interceptor [ExceptionHandlingMiddleware] removeu a necessidade de blocos `try/catch` repetitivos e redundantes dentro dos Controllers.
* Captura de forma centralizada qualquer exceção não tratada e a transforma em uma resposta HTTP padronizada no formato JSON:
  * `DomainValidationException` → 400 Bad Request (com as mensagens legíveis da validação de negócio).
  * `NotFoundException` / Recurso inexistente → 404 Not Found.
  * Exceções Inesperadas / Erro no Servidor → 500 Internal Server Error (ocultando detalhes sensíveis do stack trace contra potenciais agentes maliciosos).
