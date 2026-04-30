# PROJECT_STATUS.md

> Fonte viva de verdade sobre o estado atual do projeto.
> Se este arquivo conflitar com descricoes historicas em `README.md`, `AGENT_HANDOFF.md` ou trechos antigos de `AGENTS.md`, este arquivo prevalece para o estado atual.

## Objetivo Deste Arquivo

Este arquivo existe para reduzir tres riscos recorrentes quando outro agente de IA entra no repositorio:

- atuar com base em documentacao antiga
- repetir trabalho ja concluido
- introduzir regressao por nao entender o estagio real do produto

Nenhum agente deve iniciar mudancas relevantes sem ler:

1. `AGENTS.md`
2. `PROJECT_STATUS.md`
3. o RFC diretamente relacionado ao trabalho que vai executar

## Data De Referencia

- Ultima consolidacao manual: `2026-04-30`

## Estagio Atual Do Produto

O produto esta em um estagio de `release candidate tecnico com validacao manual final pendente`.

Traduzindo isso para o estado real:

- o editor visual principal esta funcional
- o runtime manual e o runtime continuo ja foram integrados no app desktop
- a bridge UI/backend esta alinhada com o contrato atual de runtime
- o branding operacional do produto esta consolidado em `Sidekick`, com compatibilidade legada para `Ajudante`
- o pipeline de publish oficial foi revalidado em `src/Ajudante.App/bin/publish/Sidekick.exe`; ainda exige fechar qualquer instancia em execucao antes de republicar
- `Snip` e `Mira` existem como ferramentas reutilizaveis; `Mira` agora exibe seletor, janela, processo, caminho do executavel, bounds absolutos/relativos, cursor/pixel e score de robustez
- a jornada de criacao de fluxos recebeu recipes oficiais para desktop automation, wait text then click, popup auto-confirm, Trae auto-continue, fallback visual e scheduler
- Marketplace local de recipes oficiais existe na toolbar; marketplace remoto foi avaliado em `MARKETPLACE_EVALUATION.md` e segue bloqueado para execucao irrestrita ate existir manifesto seguro, hash/assinatura, aviso de nodes sensiveis e importacao desarmada

Resumo de release em `2026-04-29`:

- P0 desktop automation implementado: filtros por `processName`, `processPath` e `windowTitleMatch`; actions `desktopWaitElement`, `desktopClickElement`, `desktopReadElementText`, `windowControl`, `waitProcess` e `clickImageMatch`; triggers `desktopElementAppeared`, `desktopElementTextChanged`, `scheduleTime`, `interval` e `processEvent`.
- Runtime endurecido com eventos de fase (`waitingForElement`, `elementMatched`, `fallbackVisualActive`, `clickExecuted`, `cooldownActive`, etc.), limite de passos por execucao e deteccao de ciclo/recursao.
- Flow oficial `flows/trae_auto_continue.json` nao usa `manualStart`; usa trigger desktop real com cooldown/debounce/max repeat e selector de processo/caminho.
- UX de criacao reforcada: palette nao fica vazia quando o registry do host falha/retorna vazio; canvas tem menu de contexto por botao direito; capturas do Mira/Snip podem criar nodes pre-preenchidos.
- Mira ajustado para inspecionar o app abaixo do overlay durante o polling e nao o proprio overlay.
- Publish oficial revalidado em `src/Ajudante.App/bin/publish` apos fechar a instancia que segurava `Sidekick.dll`.
- Limitacao residual honesta: OCR ainda nao esta funcional como produto; o fallback visual entregue nesta rodada e image matching/click por template.

Atualizacao de produto em `2026-04-30`:

- Link assistido no canvas foi endurecido: soltar um fio em qualquer area vazia do canvas abre o menu de criacao e conecta automaticamente quando as portas sao compativeis.
- Handles dos nodes ficaram maiores, visiveis como circulos de conexao em cada linha de porta, com cursor de conexao para aproximar a experiencia de n8n/Blender.
- Marketplace local agora aparece como botao dedicado na toolbar e lista recipes oficiais/nativos com busca; downloads remotos continuam bloqueados ate existir governanca segura.
- Novas actions visuais:
  - `action.overlayColor` para cobrir a tela/regiao com cor solida, timer, plano, fullscreen, opacidade, motion, fade e click-through.
  - `action.overlayImage` para lancar imagens com fit `contain/cover/stretch/none`, background, timer, plano, fullscreen, motion e fade.
  - `action.overlayText` para lancar texto com fonte, tamanho, cor, efeito, alinhamento, background, timer, plano, fullscreen, motion e fade.
- Novas actions de console/PWD:
  - `action.consoleSetDirectory` define a variavel de diretório de trabalho do fluxo.
  - `action.consoleCommand` executa comando em modo `direct`, `cmd` ou `powershell`, com timeout, working directory, stdout/stderr, exit code e porta `error`.
- Novos recipes:
  - `flows/recipe_overlay_visual_message.json`
  - `flows/recipe_console_pwd_command.json`

## O Que Ja Foi Concluido E Validado

### Runtime E Bridge

Concluido e validado:

- `FlowRuntimeManager` integrado ao host desktop
- `BridgeRouter` atualizado para suportar:
  - `runFlow`
  - `stopFlow`
  - `activateFlow`
  - `deactivateFlow`
  - `getStatus`
  - `getRuntimeStatus`
- eventos de runtime encaminhados para a UI:
  - `runtimeStatusChanged`
  - `flowArmed`
  - `flowDisarmed`
  - `triggerFired`
  - `flowQueued`
  - `flowQueueCoalesced`
  - `runtimeError`

### Branding E Persistencia

Concluido e validado:

- `ProductIdentity` usado como referencia de branding operacional
- `AppPaths.Initialize()` usado no startup
- migracao de dados legados `Ajudante` -> `Sidekick` ativa
- `WebView2` configurado para usar diretorio persistente resolvido por `AppPaths`

### Ativos Do Snip

Concluido e validado neste primeiro passo:

- diretorios oficiais de assets adicionados em `AppPaths`
- persistencia local de capturas do `Snip` em PNG
- manifesto JSON para ativos do `Snip`
- catalogo local inicial via `SnipAssetCatalog`
- listagem inicial de ativos exposta pela bridge
- bootstrap da UI para carregar o catalogo local
- bridge com resolucao de template para ativo salvo via `assets/getSnipAssetTemplate`
- seletor inline de ativos do `Snip` no painel de propriedades para `ImageTemplate`
- sample flow `flows/portfolio_snip_reuse_demo.json` cobrindo o caminho de reuse
- testes focados para persistencia, serializacao, normalizacao e consumo do payload estruturado nos projetos `Ajudante.Core.Tests` e `Ajudante.Nodes.Tests`

### Publish

Concluido e validado:

- o app pode ser publicado com sincronizacao dos assets do frontend
- o caminho publicado que foi revalidado manualmente e:
  - `src/Ajudante.App/bin/publish/Sidekick.exe`

Atencao operacional:

- ja ocorreu divergencia entre `src/Ajudante.App/bin/publish` e `src/Ajudante.App/bin/Release/net8.0-windows/publish`
- se um agente precisar validar o executavel final que o usuario vai abrir, ele deve publicar explicitamente para a pasta usada pelo usuario

Comando de referencia:

```powershell
dotnet publish .\src\Ajudante.App\Ajudante.App.csproj -c Release -o .\src\Ajudante.App\bin\publish
```

## Validacao Mais Recente Conhecida

Executada com sucesso em `2026-04-30` apos overlay/console/link assistido/Marketplace local:

- `dotnet build Ajudante.sln`
- `dotnet test Ajudante.sln --no-build`
- `npm run test` em `src/Ajudante.UI`
- `npm run build` em `src/Ajudante.UI`

Resultados:

- build .NET: `0` erros, `0` avisos
- testes .NET: `247` aprovados (`105` Core, `142` Nodes)
- testes UI: `36` aprovados
- build UI: assets gerados em `src/Ajudante.App/wwwroot` (`index-D1Xl9ODN.js`, `index-xxIiRT3a.css`)
- publish alternativo: `dotnet publish .\src\Ajudante.App\Ajudante.App.csproj -c Release -o .\src\Ajudante.App\bin\publish-overlay-rc` passou e gerou `Sidekick.exe` com assets atuais
- publish oficial: apos fechar a instancia aberta do Sidekick (PID 16208), `dotnet publish .\src\Ajudante.App\Ajudante.App.csproj -c Release -o .\src\Ajudante.App\bin\publish` passou e gerou `Sidekick.exe` com assets atuais em `src/Ajudante.App/bin/publish`

Limitacao operacional desta validacao:

- validacao manual interativa do executavel publicado ainda precisa ser feita no ambiente do usuario antes de chamar de RC final de distribuicao.

Executada com sucesso em `2026-04-29`:

- `dotnet build Ajudante.sln --no-restore`
- `dotnet test Ajudante.sln --no-build`
- `npm run test` em `src/Ajudante.UI`
- `npm run build` em `src/Ajudante.UI`
- `dotnet publish .\src\Ajudante.App\Ajudante.App.csproj -c Release -o .\src\Ajudante.App\bin\publish-rc --no-restore`
- `dotnet publish .\src\Ajudante.App\Ajudante.App.csproj -c Release -o .\src\Ajudante.App\bin\publish-ux-rc --no-restore`

Resultados de referencia:

- build .NET: `0` erros, `0` avisos
- testes .NET: `245` aprovados
- testes UI: `24` aprovados
- testes UI apos UX fix: `30` aprovados
- publish RC: `Sidekick.exe`, DLLs e `wwwroot/assets` gerados em `src/Ajudante.App/bin/publish-rc`
- publish UX RC: `Sidekick.exe`, DLLs e `wwwroot/assets` gerados em `src/Ajudante.App/bin/publish-ux-rc`

Validacao nao concluida nesta sessao:

- `dotnet build Ajudante.sln` e `dotnet test Ajudante.sln` sem `--no-restore` falharam antes de compilar por erro NuGet do ambiente de execucao: `Value cannot be null. (Parameter 'path1')`.
- `dotnet publish` no diretorio oficial `src/Ajudante.App/bin/publish` falhou porque a instancia `Sidekick (PID 38020)` estava usando os arquivos de publish.
- O executavel publicado nao foi aberto manualmente nesta rodada, pois executar software recem-publicado e encerrar a instancia existente exigem confirmacao operacional do usuario.

## Estrutura De Continuidade Implementada

Concluido:

- `PROJECT_STATUS.md` criado como fonte viva do estado atual
- `RFC_PRODUCT_STRENGTHENING.md` criado para registrar a fase de fortalecimento do produto
- `AGENTS.md` ajustado para separar regras permanentes de estado operacional atual
- `AGENT_HANDOFF.md` ajustado para refletir o runtime atual e a nova frente aprovada
- `WINDOWS_AUTOMATION_COMPLETENESS_MATRIX.md` criado para mapear cobertura Windows `existe/parcial/falta`
- `ENGINEERING_HANDOFF_WINDOWS_COMPLETENESS.md` criado para orientar a execucao da cobertura Windows ponta a ponta

Objetivo desta estrutura:

- impedir que agentes novos usem apenas documentacao historica
- reduzir retrabalho
- tornar obrigatorio registrar o estagio real antes e depois de mudancas relevantes

## Estado Atual Das Grandes Frentes

### 1. Editor Visual E Runtime

Estado: `estavel`

Ja existe:

- edicao visual de flows
- persistencia de flows
- runtime manual
- runtime continuo com triggers
- sincronizacao de estado com a UI

Ainda precisa evoluir:

- jornada guiada de criacao
- ativos reutilizaveis originados de captura e inspecao
- assistencia contextual no editor

### 2. Snip

Estado: `funcional com persistencia, catalogo inicial e reuse basico em flow`

Ja existe:

- captura de regiao via overlay
- envio do resultado para a UI
- persistencia local de imagem e manifesto
- catalogo local inicial carregado pela UI
- evento de bridge para ativo salvo apos captura
- binding de ativo salvo em `trigger.imageDetected` via seletor no `Property Panel`
- payload estruturado de `ImageTemplate` com `kind`, `assetId`, `displayName`, `imagePath` e `imageBase64`
- cobertura de testes para serializer, `FlowExecutor`, node runtime e sample flows

Ainda nao existe:

- catalogo pesquisavel
- OCR editavel persistido
- biblioteca UX completa para navegar, renomear, taggear e selecionar ativos

### 3. Mira

Estado: `funcional, mas ainda basico`

Ja existe:

- inspecao de elemento
- captura de metadados principais
- envio do resultado para a UI

Ainda nao existe:

- biblioteca de elementos inspecionados
- estrategias recomendadas de seletor
- camada formal de posicionamento absoluto vs relativo
- painel de diagnostico avancado curado para automacao

### 4. Marketplace / Pacotes

Estado: `nao iniciado`

Nao existe ainda:

- formato oficial de pacote
- manifesto versionado
- politica de permissao e compatibilidade
- catalogo local
- marketplace remoto

## Proxima Iniciativa Aprovada

A proxima fase aprovada pelo usuario e `fortalecer o produto`, com foco em quatro frentes:

1. jornada mais facil para criacao de fluxos
2. evolucao do `Snip` para biblioteca de ativos reutilizaveis
3. evolucao do `Mira` para inspecao realmente orientada a macro
4. criacao futura de um ecossistema de pacotes / marketplace

O plano detalhado desta fase esta em:

- `RFC_PRODUCT_STRENGTHENING.md`

Primeiro bloco tecnico recomendado dentro desta fase:

1. modelo persistido para ativos do `Snip`  [concluido]
2. catalogo local de ativos  [parcialmente concluido]
3. listagem inicial na UI  [concluido]
4. reaproveitamento do ativo em pelo menos um fluxo  [concluido]

## Ordem De Implementacao Recomendada

Para reduzir risco de regressao e evitar arquitetura prematura, a ordem atual recomendada e:

1. fundacao de ativos reutilizaveis para `Snip`
2. fundacao de ativos reutilizaveis e seletores robustos para `Mira`
3. jornada guiada de criacao de fluxos usando esses ativos
4. empacotamento local com manifesto, validacao e rollback
5. marketplace remoto

## Regras De Continuidade Para Agentes

### Antes De Codar

Todo agente deve:

1. confirmar o escopo exato da tarefa
2. localizar o workstream correspondente neste arquivo
3. ler o RFC da frente em questao
4. identificar contratos afetados
5. listar riscos de regressao antes de editar

### Ao Codar

Todo agente deve:

- evitar alterar contrato de bridge sem atualizar a documentacao correspondente
- evitar mudar formato persistido sem testes de serializacao e compatibilidade
- evitar marcar algo como concluido sem validacao real
- preferir evolucoes pequenas, testaveis e reversiveis

### Antes De Encerrar

Todo agente deve atualizar, se aplicavel:

- `PROJECT_STATUS.md` se mudou o estagio real do projeto
- o RFC relevante se o plano foi refinado ou reescopado
- `AGENT_HANDOFF.md` se houver informacoes operacionais uteis para o proximo agente

## Contratos Sensiveis

Mudancas nessas areas exigem cuidado extra:

- protocolo `BridgeMessage`
- schemas em `src/Ajudante.UI/src/bridge/types.ts`
- serializacao de flow em `Ajudante.Core`
- `FlowRuntimeManager` e seu ciclo de vida
- caminhos e migracao em `AppPaths`
- pipeline de publish em `Ajudante.App.csproj`
- flows demo em `flows/`
- testes de contrato em `tests/Ajudante.Core.Tests` e `tests/Ajudante.Nodes.Tests`

## Definicao De "Concluido"

Uma frente so pode ser considerada concluida quando:

- o escopo e os limites estao documentados
- os contratos afetados estao claros
- as validacoes automatizadas relevantes passaram
- o impacto no executavel publicado foi checado quando aplicavel
- o estado novo foi registrado neste arquivo

## O Que Nao Deve Acontecer

Nenhum agente deve:

- assumir que um RFC antigo ainda descreve o estado atual
- usar apenas `README.md` para entender o projeto
- redesenhar grandes contratos sem registrar antes escopo e criterios de aceite
- introduzir automacao "magica" sem fallback, telemetria local ou mensagem clara ao usuario
