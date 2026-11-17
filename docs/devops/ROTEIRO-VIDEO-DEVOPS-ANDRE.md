# Gravação de DevOps: PaperTrail

André, este roteiro usa a API .NET para demonstrar a entrega de DevOps. A infraestrutura está publicada, as pipelines já executaram CI e Release, e o código atual foi validado com Azure SQL real. Durante o vídeo, uma alteração pequena no código passa por PR, testes e publicação. Depois, os CRUDs são demonstrados na aplicação online e conferidos diretamente no banco.

Use este roteiro para DevOps. Os vídeos finais das disciplinas Java e .NET terão outros critérios e serão preparados separadamente.

## Antes de marcar a gravação

Combine com Orlando um horário para ele aprovar a PR para `main` na etapa 7. Essa revisão é obrigatória na política do projeto; André não precisa usar a conta de Orlando nem alterar a proteção da branch.

Grave com voz e resolução mínima de 720p. Prefira 1080p para deixar código e resultados SQL legíveis. As legendas não substituem a narração. O bloco de CRUD deve ser contínuo, mostrando cada operação e o SELECT correspondente, sem cortes para esconder erros.

Faça um ensaio antes de começar. A espera pela pipeline faz parte do fluxo; não clique em Run pipeline para substituir o disparo automático.

## 1. Preparar os acessos

Use a conta FIAP `RM565592@fiap.com.br` no Azure e no Azure DevOps. Ela recebeu Basic, Contributors no PaperTrail e leitura dos recursos do grupo da aplicação. No SQL, pode consultar preparativos e itens; as alterações dos dados serão feitas pela API.

Instale no computador:

- Git para Windows, com Git Bash: <https://git-scm.com/downloads/win>.
- Azure CLI: <https://learn.microsoft.com/cli/azure/install-azure-cli-windows>.
- SDK .NET 10: <https://dotnet.microsoft.com/download/dotnet/10.0>.

Abra o Git Bash e execute:

```bash
az login --tenant 11dbbfe2-89b8-4549-be10-cec364e59551 --allow-no-subscriptions
az account set --subscription 44b8b12c-4907-4842-9af8-2ea45fc8d44a
```

No navegador que abrir, entre com a conta FIAP do André. Volte ao terminal após a confirmação.

Cole esta função no Git Bash. Ela autentica os comandos Git no Azure usando uma credencial temporária do Azure CLI:

```bash
gitazure() {
  local ado_token
  ado_token="$(az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv | tr -d '\r')" || return
  [[ -n "$ado_token" ]] || return 1
  GIT_AUTH_HEADER="Authorization: Bearer $ado_token" \
    git --config-env=http.extraheader=GIT_AUTH_HEADER "$@"
}
```

Clone o repositório em uma pasta de trabalho sua:

```bash
gitazure clone --ipv4 --branch develop \
  https://dev.azure.com/RM561584/PaperTrail-GS-2026-2/_git/PaperTrail-DotNet \
  PaperTrail-DotNet

cd PaperTrail-DotNet
git config user.name "André Emygdio Ferreira"
git config user.email "RM565592@fiap.com.br"
git status --short --branch
```

O resultado deve indicar `develop`, sem arquivos alterados. Se já tiver esse clone, entre nele e atualize com `gitazure pull --ipv4 --ff-only origin develop`. Use a revisão entregue por Orlando com `scripts/allow-video-sql.ps1` e este roteiro já presentes no clone.

Para liberar o acesso SQL desse computador, execute na raiz do clone:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/allow-video-sql.ps1
```

O script descobre seu IPv4 e atualiza somente a regra `andre-video`. Você não precisa enviar o IP ao Orlando. Aguarde até cinco minutos para a regra ser aplicada. Se trocar de rede, execute novamente.

## 2. Deixar as abas prontas

Abra estas páginas e faça login antes de gravar:

| Página | Endereço |
| --- | --- |
| Projeto Azure DevOps | <https://dev.azure.com/RM561584/PaperTrail-GS-2026-2> |
| Boards | <https://dev.azure.com/RM561584/PaperTrail-GS-2026-2/_boards/board> |
| Código .NET | <https://dev.azure.com/RM561584/PaperTrail-GS-2026-2/_git/PaperTrail-DotNet> |
| Pipelines | <https://dev.azure.com/RM561584/PaperTrail-GS-2026-2/_build> |
| PR de publicação .NET | <https://dev.azure.com/RM561584/PaperTrail-GS-2026-2/_git/PaperTrail-DotNet/pullrequest/4> |
| Portal Azure | <https://portal.azure.com/> |
| Swagger Java | <https://papertrail-java-rm561584.azurewebsites.net/swagger-ui/index.html> |
| Swagger .NET | <https://papertrail-dotnet-rm561584.azurewebsites.net/swagger> |

No Portal Azure, abra o grupo `rm561584-papertrail-gs`. Depois abra o banco `papertrail`, do servidor `papertrail-sql-rm561584`, e selecione **Query editor (preview)**. Escolha Microsoft Entra e **Continue as RM565592@fiap.com.br**.

Confirme a conexão executando:

```sql
SELECT TOP (5) ID, SERVICE_ID, TITLE
FROM dbo.PT_PREPARATION
ORDER BY ID DESC;
```

Confira também que o projeto no DevOps abre Boards, Repos e Pipelines. Se houver erro de acesso, resolva no ensaio antes de gravar. Novas permissões podem levar alguns minutos para aparecer; sair e entrar novamente pode ser necessário.

Leia o README e abra `docs/devops/PaperTrail_rm561584.pdf`. A página 2 contém a arquitetura. O vídeo seguirá a mesma solução: Azure Repos, Boards, CI, artefato, Release, App Service e Azure SQL.

As rotas novas de itens entram no ambiente público com a publicação demonstrada no vídeo. O ensaio já validou essas rotas com o Azure SQL real. Na gravação, espere a Release terminar antes de iniciar o CRUD.

## 3. Apresentar a solução e a infraestrutura

Comece com o README e o desenho da arquitetura na tela.

Fala sugerida:

> O PaperTrail ajuda o cidadão a organizar os requisitos antes de um atendimento público. O Java fornece autenticação e catálogo, e o .NET mantém os preparativos e seus itens. As aplicações rodam em dois App Services Linux no mesmo plano B2 e persistem os dados no Azure SQL. Nesta gravação vamos acompanhar uma mudança desde a tarefa até a aplicação publicada.

Percorra as setas do desenho: usuário e APIs; .NET consultando o catálogo Java; APIs acessando SQL; repositório, CI, pacote e Release.

No Portal Azure, mostre o grupo `rm561584-papertrail-gs`, a região Mexico Central, o plano `asp-papertrail-rm561584`, os dois App Services e o banco `papertrail`.

No repositório, abra `scripts/provision-azure.sh`, `scripts/script-bd.sql` e `azure-pipeline.yml`. Mostre os comandos de criação dos recursos e o DDL. Os recursos já foram provisionados; não execute o instalador do banco nem o provisionamento novamente durante a gravação.

No YAML, destaque o gatilho da `main`, os estágios CI e Release, a publicação de TRX, o artefato `api` e o download do mesmo pacote na Release.

> A main é protegida por PR, revisor obrigatório e vínculo com Work Item. O CI compila, testa e gera um ZIP. Após o CI aprovado, a Release usa esse mesmo ZIP e publica no App Service. A conexão com a Azure usa identidade federada.

Mantenha valores de configurações protegidas ocultos. Essa demonstração .NET não precisa de chave OpenAI.

## 4. Criar a tarefa no Boards

Abra **Boards → Work items → New work item → Task**. Use o título `Demonstrar publicação da API .NET do PaperTrail` e atribua a você. Na descrição, informe que a alteração vai esclarecer a descrição da consulta de preparativos e será demonstrada no Swagger publicado.

Salve a tarefa e anote seu número. Nos próximos comandos, troque `123` por esse número real. Deixe a tarefa em **In Progress**.

## 5. Criar uma branch e alterar o código

Na raiz do clone .NET, no Git Bash:

```bash
git switch develop
gitazure pull --ipv4 --ff-only origin develop
git switch -c feature/devops-video-123
```

Abra `PaperTrail.API/Endpoints/PreparationEndpoints.cs` no editor. Localize:

```csharp
.WithSummary("List your preparations")
```

Troque apenas essa descrição por:

```csharp
.WithSummary("List preparations for the authenticated user")
```

Essa alteração está no código C# e será visível no Swagger. Preserve os métodos e as regras de negócio.

No terminal, mostre a diferença e rode os testes:

```bash
git diff -- PaperTrail.API/Endpoints/PreparationEndpoints.cs
dotnet test PaperTrail.sln --configuration Release
```

Se os testes passarem:

```bash
git add PaperTrail.API/Endpoints/PreparationEndpoints.cs
git commit -m "docs(api): clarify preparation ownership AB#123"
gitazure push --ipv4 -u origin feature/devops-video-123
```

Mostre o commit na aba Repos do Azure DevOps. Não publique diretamente na `main`.

## 6. Integrar a alteração na develop

Em **Repos → Pull requests**, crie uma PR da `feature/devops-video-123` para `develop`. Confira os nomes de origem e destino antes de criar. Vincule a tarefa criada no vídeo no campo **Work items**.

Mostre o arquivo C# alterado na aba **Files** e conclua essa PR. Pode excluir a branch `feature/devops-video-123` após o merge. A `develop` deve ser preservada.

Essa integração ainda não publica a aplicação. O gatilho de publicação acompanha a `main`.

## 7. Fazer a PR para main

Abra a PR .NET 4, indicada na tabela de links. Ela promove `develop` para `main` e já reúne a versão validada de preparativos, itens, progresso, rate limiting e health checks.

Confira que a alteração gravada está incluída na aba Files. Adicione a tarefa do vídeo em **Work items**, mantendo os vínculos existentes. Tire a PR do estado **Draft** usando **Publish** ou **Mark as ready**.

Peça a aprovação de Orlando, o revisor obrigatório RM561584. Quando as políticas estiverem aprovadas, use **Complete**. Não marque a exclusão da branch `develop` e não use bypass de políticas.

> A alteração está vinculada à tarefa e foi revisada. O merge entrou na main, e agora o gatilho inicia o CI automaticamente.

Se essa PR já tiver sido concluída antes do ensaio, crie outra de `develop` para `main` com a tarefa do vídeo e siga as mesmas políticas. O vídeo precisa mostrar o merge da alteração que você acabou de fazer.

## 8. Acompanhar CI, testes, pacote e Release

Abra **Pipelines → PaperTrail-DotNet** e entre na execução nova. Confira que ela corresponde ao commit da `main` e foi iniciada por CI, sem usar o botão Run pipeline.

Mostre os passos de restauração, build, testes, publicação da API e geração do pacote. Na aba **Tests**, mostre o resultado, sem testes reprovados. Os testes de CI usam SQLite e substitutos HTTP; os testes no banco real são demonstrados pelo CRUD a seguir.

Abra o artefato **api**. O pacote é `PaperTrail.DotNet.zip` e contém a aplicação publicada e o script de inicialização. Não abra arquivos de credenciais.

Mostre o estágio **Release / Deploy API**, incluindo o download do artefato, a publicação no App Service e a verificação HTTPS. Aguarde o resultado final **Succeeded**.

> O CI publicou os testes e o pacote. A Release baixou o pacote dessa mesma execução e atualizou o App Service. O banco e as configurações protegidas permanecem no ambiente da Azure.

## 9. Comprovar a alteração online

Abra o Swagger .NET público e atualize com Ctrl+F5. Localize **GET /preparativos**. A descrição deve mostrar `List preparations for the authenticated user`, igual ao código alterado no vídeo.

Abra também <https://papertrail-dotnet-rm561584.azurewebsites.net/health>. O esperado é HTTP 200 e:

```json
{"status":"Healthy","checks":{"database":"Healthy","catalog":"Healthy"}}
```

Use sempre o endereço `azurewebsites.net` para os CRUDs. A demonstração de publicação não usa localhost.

## 10. Preparar o login da API

No Swagger Java, cadastre uma conta fictícia de demonstração em **POST /auth/register**. Informe `name`, um e-mail de teste exclusivo e uma senha própria para esse ensaio. O cadastro cria o papel `CITIZEN`, suficiente para os preparativos.

Faça login em **POST /auth/login** e copie o campo `token`. No botão **Authorize** do Swagger .NET, cole somente o token, sem escrever `Bearer` antes. Use a mesma conta durante todo o CRUD.

No Swagger Java, consulte **GET /services** e anote um `id` existente. O exemplo abaixo usa `serviceId: 1`; confirme esse ID na consulta antes de enviar.

Daqui até o final dos DELETEs, grave sem cortes. Anote os IDs retornados; não use IDs de registros de outros integrantes.

## 11. Criar e consultar o preparativo

No Swagger .NET, abra **POST /preparativos → Try it out**. No cabeçalho `Idempotency-Key`, informe `video-andre-123-01`, trocando 123 pelo número da tarefa.

No corpo:

```json
{"serviceId":1,"title":"Video Andre - preparativo"}
```

Execute. Mostre HTTP **201**, o cabeçalho Location e anote o `id` retornado como **ID DO PREPARATIVO**. Faça **GET /preparativos/{id}** usando esse número.

No Query Editor, substitua 1000 pelo ID real e execute:

```sql
SELECT ID, OWNER_SUBJECT, SERVICE_ID, SOURCE_VERSION, TITLE, CREATED_AT
FROM dbo.PT_PREPARATION
WHERE ID = 1000;
```

O ID e o título devem corresponder à resposta HTTP. O preparativo recebe os requisitos do catálogo como itens. Confira-os em outra consulta, com o mesmo ID:

```sql
SELECT ID, PREPARATION_ID, REQUIREMENT_ID, TITLE, REQUIRED, COMPLETED
FROM dbo.PT_PREPARATION_ITEM
WHERE PREPARATION_ID = 1000
ORDER BY ID;
```

Em cada ensaio, use uma chave de idempotência nova. Repetir uma chave antiga pode devolver a resposta de um recurso já excluído; isso é o comportamento esperado da idempotência.

## 12. Criar e consultar um item pessoal

Abra **POST /preparativos/{preparationId}/itens**, preencha o ID do preparativo e envie:

```json
{"title":"Documento do video Andre","required":true}
```

Mostre HTTP **201** e anote o `id` como **ID DO ITEM**. Consulte-o em **GET /preparativos/{preparationId}/itens/{itemId}**.

No Query Editor, troque 2000 pelo ID real do item:

```sql
SELECT ID, PREPARATION_ID, REQUIREMENT_ID, TITLE, REQUIRED, COMPLETED
FROM dbo.PT_PREPARATION_ITEM
WHERE ID = 2000;
```

Mostre que `PREPARATION_ID` corresponde ao preparativo criado e que o título está gravado. O `REQUIREMENT_ID` desse item pessoal é nulo.

## 13. Atualizar as duas tabelas

Em **PUT /preparativos/{id}**, use o ID anotado e envie:

```json
{"title":"Video Andre - preparativo atualizado"}
```

Mostre HTTP **200** e execute novamente o SELECT do preparativo pelo ID. O título deve ter mudado.

Em **PUT /preparativos/{preparationId}/itens/{itemId}**, use os dois IDs e envie:

```json
{"title":"Documento conferido no video","required":true}
```

Mostre HTTP **200** e execute o SELECT do item pelo ID. O título deve estar atualizado.

Para demonstrar o estado, em **PATCH /preparativos/{preparationId}/itens/{itemId}/estado**, envie:

```json
{"completed":true}
```

Mostre HTTP **200** e o valor `COMPLETED = 1` no SELECT. Pode consultar **GET /preparativos/{preparationId}/progresso** para mostrar o cálculo; um único item concluído não garante 100%, pois o preparativo pode ter outros obrigatórios.

## 14. Excluir e comprovar no banco

Primeiro execute **DELETE /preparativos/{preparationId}/itens/{itemId}** para o item pessoal do vídeo. Mostre HTTP **204** e repita o SELECT `WHERE ID = 2000`, com o ID correto. O resultado deve ter zero linhas.

Depois execute **DELETE /preparativos/{id}** para o preparativo do vídeo. Mostre HTTP **204** e repita o SELECT do preparativo. O resultado também deve ter zero linhas.

Execute o SELECT de itens filtrado por `PREPARATION_ID`. Ele deve retornar zero linhas, confirmando que os itens copiados do catálogo também foram removidos.

> Demonstramos criação, consulta, atualização e exclusão nas tabelas de preparativos e itens. Os SELECTs foram executados diretamente no Azure SQL e comprovaram os resultados das chamadas à API publicada.

O edital pede SELECT como prova da persistência. Não substitua essa parte mostrando apenas o GET do Swagger.

## 15. Concluir a tarefa e encerrar

Volte à tarefa criada no vídeo. Mostre os vínculos com branch, commit e PRs. Se algum vínculo não aparecer, use **Add link** e associe o artefato correto antes de concluir. Passe a tarefa para **Done** e salve.

Mostre novamente a execução CI/Release aprovada e os links das aplicações. A tarefa 2, usada pela equipe para o fechamento geral, é separada da tarefa que você criou para a gravação.

Após gravar, feche o acesso SQL do computador:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/allow-video-sql.ps1 -Close
```

Essa opção mantém a regra com um endereço reservado e preserva as regras dos App Services. Para repetir um ensaio, execute o script novamente sem `-Close`.

Assista ao vídeo inteiro antes de enviar. Confira voz, resolução e a sequência tarefa → código → PR → main → CI/testes/artefato → Release → Swagger atualizado → CRUD/SELECT → tarefa concluída.

Envie a URL do YouTube ao Orlando. Ele vai acrescentar o link ao PDF e ao arquivo de identificação e preparar a PR final de documentação. Essa PR final não substitui a alteração de código e a publicação demonstradas no vídeo.

## Se aparecer um erro

| Situação | O que conferir |
| --- | --- |
| Azure Repos pede senha ou perde a conexão | Use `gitazure` para clone, pull e push; a função precisa ser colada novamente se abrir outro Git Bash. |
| Script SQL retorna AuthorizationFailed | Confira `az account show --query user.name -o tsv`; deve ser a conta FIAP do André. Aguarde a propagação das permissões e fale com Orlando se persistir. |
| Query Editor informa IP bloqueado | Execute `allow-video-sql.ps1` sem `-Close`, espere até cinco minutos e conecte novamente. |
| Query Editor não autentica | Use Microsoft Entra com RM565592 e o banco papertrail. A senha de login da API não é credencial SQL. |
| PR não permite Complete | Confira Work Item e aprovação do revisor obrigatório. Não desative a política. |
| Pipeline falhou | Abra a primeira tarefa vermelha e corrija antes de gravar novamente. Não apresente uma execução antiga como prova do commit novo. |
| Swagger não mostra a descrição nova | Espere Release Succeeded, confira o commit, use a URL pública e atualize com Ctrl+F5. |
| API retorna 401 | Renove o token no login Java e atualize Authorize no .NET. |
| POST retorna 409 ou repete um ID antigo | Use outra Idempotency-Key para o novo ensaio. |
| API retorna 429 | Respeite os segundos indicados no cabeçalho Retry-After. |

Referência: edital Global Solution 2026/2, páginas 38 a 44 e 46 a 50. O roteiro cobre as duas tabelas de negócio relacionadas da API .NET; o banco também contém os objetos do contexto Java e tabelas técnicas de suporte.
