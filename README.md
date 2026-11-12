# PaperTrail .NET

API de preparativos e checklists do PaperTrail. A autenticação usa o JWT emitido
pela API Java.

## Estrutura

- `PaperTrail.Domain`: entidades e validações do domínio.
- `PaperTrail.Application`: DTOs, contratos de repositórios e serviços.
- `PaperTrail.Infrastructure`: EF Core, mapeamentos Oracle, repositórios e cliente HTTP do catálogo.
- `PaperTrail.API`: Minimal APIs, autenticação, Swagger e registro das dependências.
- `PaperTrail.Domain.Tests` e `PaperTrail.API.Tests`: testes de domínio e integração HTTP.

## Executar

Use o SDK .NET 10. Confira a versão com `dotnet --version`.
Se ele estiver instalado em `$HOME/.dotnet`, ajuste o PATH no Git Bash:

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet --version
```

As tabelas são instaladas pelo script `docs/database/papertrail.sql`. Se você já
executou esse script para o Java na mesma conta Oracle, as tabelas já estão prontas.
A aplicação não executa migrations nem cria ou exclui tabelas ao iniciar.

No terminal, dentro do repositório, configure a conexão sem gravar a senha no código:

```bash
read -r -s -p "Oracle connection string: " ConnectionStrings__PaperTrailOracle
export ConnectionStrings__PaperTrailOracle
```

Formato da conexão:

```text
User Id=seu_usuario;Password=sua_senha;Data Source=//oracle.fiap.com.br:1521/ORCL
```

Configure o caminho do arquivo de chave pública usado pelo Java. Com os repositórios
nas pastas `java/PaperTrail` e `dotnet/PaperTrail`, e o par RSA em `.local` no Java:

```bash
export Jwt__PublicKeyPath="$(cd ../../java/PaperTrail && pwd -W)/.local/public.pem"
export JavaCatalog__BaseUrl="http://localhost:8080/"
dotnet run --project PaperTrail.API
```

A .NET recebe somente a chave pública correspondente ao par configurado no Java.
`Jwt__PublicKeyPath` é um caminho de arquivo, sem o prefixo `file:`.

O Swagger fica em `http://localhost:5180/swagger`. Obtenha um token em
`POST /auth/login` no Java e informe seu valor no botão **Authorize**.

## Endpoints disponíveis

| Método | Rota | Uso |
| --- | --- | --- |
| GET | `/preparativos?page=1&pageSize=20` | Listar os preparativos do usuário autenticado |
| GET | `/preparativos/{id}` | Consultar um preparativo do usuário autenticado |
| POST | `/preparativos` | Criar um preparativo com os itens do catálogo Java |

A paginação começa em 1 e aceita até 100 registros por página.
As rotas exigem o papel `CITIZEN` ou `MANAGER`. O proprietário vem do `sub` do JWT;
um gestor também consulta apenas os próprios preparativos. Recursos de outro usuário
retornam o mesmo 404 de um recurso inexistente.

Os tokens precisam usar RS256, emissor `papertrail`, audiência `papertrail-api`,
expiração válida, `sub` com o ID positivo do usuário e a lista `roles`.

A carga de demonstração do banco usa proprietários fictícios. Um usuário recém-criado
no Java recebe uma lista vazia até criar seus preparativos.

## Criar um preparativo

Envie o token Java, uma chave de idempotência e o serviço desejado:

```http
POST /preparativos
Authorization: Bearer <token>
Idempotency-Key: preparativo-001
Content-Type: application/json

{"serviceId":1,"title":"Meu atendimento"}
```

O proprietário vem do JWT. A versão e os itens vêm do snapshot do Java, incluindo
os requisitos opcionais. Todos os itens começam sem conclusão. O retorno é 201,
com o cabeçalho `Location` apontando para o preparativo criado.

`Idempotency-Key` é obrigatório: um valor de 1 a 128 caracteres ASCII visíveis,
sem espaços ou vírgulas. A chave vale para o usuário e a operação de criação.
Repetir a mesma chave e os mesmos valores devolve o status, o corpo e o `Location`
originais, sem consultar o Java novamente. O título é comparado após remover
espaços nas pontas. Alterar o título ou `serviceId` usando a mesma chave retorna 409.

O snapshot é consultado antes da transação de escrita. Preparativo, itens e resposta
idempotente são gravados juntos. Se a criação falhar, a transação é revertida e a
chave pode ser usada em uma nova tentativa. Requisições concorrentes com a mesma
chave mantêm uma única criação confirmada no banco.

## Integração com Java

`IJavaCatalogClient` consulta `GET /services/{id}/requirements-snapshot`, encaminhando
o token da requisição. O retorno inclui serviço, versão e requisitos com IDs estáveis.
O cliente valida os campos recebidos e rejeita respostas incompletas ou inconsistentes.

O contexto `PaperTrailContext` mapeia `PT_PREPARATION`, `PT_PREPARATION_ITEM` e
`PT_SERVICE_VERSION`. Os identificadores do catálogo são referências externas.
`IPreparationRepository` faz consultas por ID e proprietário; `IServiceVersionRepository`
consulta a versão conhecida de cada serviço.

## Testes

```bash
dotnet test PaperTrail.sln
```

Os testes geram um par RSA temporário e usam persistência e respostas HTTP locais.
Não precisam de senha Oracle, chave privada do Java ou serviços externos em execução.
Os testes de mapeamento conferem a tradução SQL para Oracle; a execução no banco real
é uma verificação separada.
Os testes de criação usam SQLite temporário para conferir transações, concorrência,
restrição de chave única e rollback da auditoria. A validação específica no Oracle
é feita separadamente, em tabelas isoladas de teste.

## Próximas etapas

Edição e exclusão de preparativos ainda serão implementadas. Também estão pendentes os endpoints de itens
e progresso, rate limiting, health checks e o consumidor de eventos.

Os arquivos de Database ficam em `docs/database`, com o mesmo conteúdo do repositório
Java. O PDF ainda aguarda a consolidação final da disciplina.
