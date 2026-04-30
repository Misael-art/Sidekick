# Handoff Tecnico Para Outro Agente de IA

> Ordem obrigatoria de leitura antes de atuar:
> `AGENTS.md` -> `PROJECT_STATUS.md` -> RFC da frente ativa -> este handoff.
> Este arquivo e operacional e historico. Para estado atual consolidado, prevalece `PROJECT_STATUS.md`.

## Visao Geral

Este projeto implementa uma ferramenta de automacao visual para Windows, no estilo RPA leve, em que o usuario monta fluxos conectando nos em uma interface grafica em vez de escrever codigo.

O produto combina:

- um host desktop WPF com WebView2
- uma UI React/TypeScript para edicao visual dos fluxos
- um core .NET para execucao, serializacao e validacao
- uma biblioteca de nos embutidos
- uma camada de integracao nativa com recursos do Windows

Em termos práticos, pense no sistema como:

1. editor visual de fluxos
2. runtime de automacao desktop
3. bridge entre frontend web e backend desktop

## Objetivo Do Produto

O projeto se propoe a permitir que usuarios criem automacoes locais no Windows usando um editor visual baseado em grafo.

As automacoes podem envolver:

- mouse e teclado
- arquivos e pastas
- notificacoes desktop
- HTTP e integracoes de dados
- email
- leitura de tela e captura de imagem
- monitoramento de eventos do sistema

Os fluxos sao compostos por tres categorias de nos:

- `Trigger`: inicia a automacao
- `Logic`: controla fluxo, condicoes, loops e variaveis
- `Action`: executa uma acao concreta

## Estrutura Da Solucao

Arquivo principal da solucao:

- `Ajudante.sln`

Projetos principais:

- `src/Ajudante.App`: host WPF, WebView2, tray, bridge e ciclo de vida do app
- `src/Ajudante.UI`: frontend React + Vite com editor visual
- `src/Ajudante.Core`: modelos, serializacao, validacao, registry e executor
- `src/Ajudante.Nodes`: nos embutidos de acao, logica e gatilho
- `src/Ajudante.Platform`: integracoes nativas com Windows
- `tests/Ajudante.Core.Tests`: testes do core, serializer, validator, registry e sample flows
- `tests/Ajudante.Nodes.Tests`: testes dos nos e da bridge

## Arquitetura Em Camadas

### 1. App Desktop

O host WPF sobe a aplicacao, inicializa o WebView2, carrega o frontend e conecta o backend com a UI.

Pontos centrais:

- `src/Ajudante.App/App.xaml.cs`
- `src/Ajudante.App/MainWindow.xaml.cs`
- `src/Ajudante.App/Bridge/WebBridge.cs`
- `src/Ajudante.App/Bridge/BridgeRouter.cs`
- `src/Ajudante.App/TrayIcon/SystemTrayManager.cs`

Responsabilidades:

- garantir instancia unica
- preparar diretorios de dados e migracao legada
- registrar tratamento global de excecoes
- inicializar o registro de nos e plugins
- iniciar o runtime de flows
- hospedar a UI React em WebView2
- atuar como ponte entre comandos da UI e servicos do backend
- administrar o tray icon

### 2. Frontend

O frontend usa React e `@xyflow/react` para montar o editor visual.

Pontos centrais:

- `src/Ajudante.UI/src/App.tsx`
- `src/Ajudante.UI/src/store/flowStore.ts`
- `src/Ajudante.UI/src/bridge/bridge.ts`
- `src/Ajudante.UI/src/bridge/flowConverter.ts`
- `src/Ajudante.UI/src/bridge/types.ts`

Responsabilidades:

- renderizar toolbar, palette, canvas e painel de propriedades
- manter estado do flow em edicao
- converter o flow visual para o formato backend
- enviar comandos para o host WPF
- receber eventos de execucao e refletir status/logs na UI

### 3. Core

O core contem o dominio principal do sistema.

Pontos centrais:

- `src/Ajudante.Core/Models/Flow.cs`
- `src/Ajudante.Core/Models/NodeInstance.cs`
- `src/Ajudante.Core/Engine/FlowExecutor.cs`
- `src/Ajudante.Core/Engine/TriggerManager.cs`
- `src/Ajudante.Core/Registry/NodeRegistry.cs`
- `src/Ajudante.Core/Serialization/FlowSerializer.cs`

Responsabilidades:

- modelar flow, conexoes, variaveis e status
- instanciar nos a partir do `typeId`
- validar e serializar flows
- executar o grafo de forma assíncrona
- fornecer contratos para plugins e nos embutidos

### 4. Nodes

Os nos embutidos ficam em `src/Ajudante.Nodes`.

Subareas:

- `Actions`: mouse, teclado, arquivos, HTTP, email, notificacoes
- `Logic`: template, variaveis, comparacao, filtro, retry, delay, loop
- `Triggers`: hotkey, file system, image detection, pixel change, manual start, window event

Cada no:

- implementa uma interface do core
- expõe um `NodeDefinition`
- recebe propriedades configuradas pela UI
- executa sua logica via `ExecuteAsync`

### 5. Platform

`src/Ajudante.Platform` encapsula integracoes com Windows:

- simulacao de mouse e teclado
- clipboard
- notificacoes
- captura de tela
- leitura de pixel
- UIAutomation
- watchers de janelas e processos

## Fluxo De Inicializacao

Na inicializacao:

1. `App.xaml.cs` chama `AppPaths.Initialize()`, garante instancia unica e prepara `%AppData%\Sidekick`
2. o app cria ou migra diretorios de `flows`, `logs`, `plugins` e `WebView2Data`
3. flows empacotados em `seed-flows` sao copiados para a pasta do usuario, se ainda nao existirem
4. `MainWindow.xaml.cs` cria `NodeRegistry`
5. o registro carrega nos embutidos e plugins externos
6. `FlowRuntimeManager` e `WebBridge` sao inicializados
7. o frontend React e carregado no WebView2
8. `BridgeRouter` passa a responder comandos da UI
9. a UI solicita definicoes de nos e estado do engine

## Contrato Da Bridge

O frontend conversa com o backend usando mensagens JSON.

Tipo base:

- `type`: `command`, `event` ou `response`
- `channel`: `flow`, `engine`, `platform`, `inspector`, `registry`
- `action`: nome da operacao
- `requestId`: correlacao de request/response
- `payload`: dados
- `error`: erro, quando houver

Arquivos principais:

- `src/Ajudante.UI/src/bridge/types.ts`
- `src/Ajudante.UI/src/bridge/bridge.ts`
- `src/Ajudante.App/Bridge/WebBridge.cs`
- `src/Ajudante.App/Bridge/BridgeRouter.cs`

Fluxo basico:

1. React envia um `command`
2. `WebBridge` recebe no WebView2
3. `BridgeRouter` roteia por `channel` e `action`
4. o backend responde com `response` ou emite `event`
5. o frontend atualiza store e UI

## Acoes Principais Expostas Pela Bridge

### Channel `flow`

- `saveFlow`
- `loadFlow`
- `listFlows`
- `newFlow`
- `deleteFlow`

### Channel `engine`

- `runFlow`
- `stopFlow`
- `getStatus`
- `activateFlow`
- `deactivateFlow`
- `getRuntimeStatus`

### Channel `platform`

- `startMira`
- `startSnip`
- `cancelInspector`

### Channel `assets`

- `listSnipAssets`
- `getSnipAssetTemplate`

### Channel `registry`

- `getNodeDefinitions`

## Modelo De Flow

Um flow persistido contem:

- `id`
- `name`
- `version`
- `variables`
- `nodes`
- `connections`
- `createdAt`
- `modifiedAt`

Cada `NodeInstance` contem:

- `id`
- `typeId`
- `position`
- `properties`

Os arquivos de flow sao JSON. Exemplos reais:

- `flows/portfolio_notification_demo.json`
- `flows/portfolio_email_demo.json`
- `flows/portfolio_file_demo.json`

## Como A UI Representa O Flow

No frontend, o canvas usa `React Flow`.

Formato visual:

- nodes com `data.propertyValues`
- edges com `sourceHandle` e `targetHandle`

Formato backend:

- `NodeInstance`
- `Connection`

Conversao:

- `toBackendFlow(...)` transforma o estado visual em payload persistivel
- `fromBackendFlow(...)` reconstrói o canvas a partir do JSON do backend

Arquivo-chave:

- `src/Ajudante.UI/src/bridge/flowConverter.ts`

## Como A Execucao Funciona

No estado atual do app, a camada principal de orquestracao e o `FlowRuntimeManager`, que usa o `FlowExecutor` internamente.

O `FlowRuntimeManager`:

1. recebe execucoes manuais e disparos de trigger
2. controla fila de execucao e coalescencia
3. ativa e desativa gatilhos de flows
4. publica snapshot de status para a UI
5. encaminha a execucao para o `FlowExecutor`

O `FlowExecutor`:

1. cria instancias reais dos nos a partir do registro
2. monta um mapa de adjacencia das conexoes
3. encontra o no de entrada
4. percorre o grafo executando `ExecuteAsync`
5. armazena outputs no contexto
6. propaga execucao pelos ports de saida
7. emite eventos de status, log, erro e conclusao

Arquivo-chave:

- `src/Ajudante.Core/Engine/FlowExecutor.cs`

Eventos relevantes:

- `NodeStatusChanged`
- `LogMessage`
- `FlowCompleted`
- `FlowError`

Esses eventos sao consumidos pelo runtime e pelo `MainWindow`, que os reenviam para a UI e tambem atualizam o estado do tray.

## Estado Atual Da Execucao De Triggers

Os triggers continuos estao integrados ao host principal.

Estado atual confirmado:

- `engine/activateFlow` arma um flow no runtime
- `engine/deactivateFlow` desarma um flow especifico
- `engine/getRuntimeStatus` retorna snapshot consolidado
- o runtime emite eventos como `flowArmed`, `flowDisarmed`, `triggerFired`, `flowQueued` e `runtimeStatusChanged`

Para outro agente, isso significa que:

- a experiencia atual nao e apenas manual
- existem dois modos reais de operacao: execucao manual e flows armados por trigger
- qualquer alteracao nessa area precisa considerar fila, coalescencia e ciclo de vida do runtime

## Sistema De Plugins

Plugins sao DLLs .NET 8 colocadas em:

- `%AppData%\Sidekick\plugins`

O `NodeRegistry`:

- varre assemblies
- identifica classes com `NodeInfoAttribute`
- gera `NodeDefinition`
- cria instancias por `typeId`

Os plugins sao carregados com `AssemblyLoadContext` isolado para reduzir locking de arquivo.

Isso facilita:

- extensao do catalogo de nos
- atualizacao de plugins
- desenvolvimento de capacidades especificas fora do core

## Persistencia E Diretorios Reais

Diretorio raiz de dados do usuario:

- `%AppData%\Sidekick`

Subpastas:

- `flows`
- `logs`
- `plugins`
- `WebView2Data`

Ponto importante:

- o codigo usa branding real `Sidekick` em varios pontos internos
- mas o repositorio e namespaces usam `Ajudante`
- existe migracao automatica de dados legados `Ajudante` -> `Sidekick` no startup

Isso pode confundir buscas, logs e manutencao.

## Branding E Nomenclatura

Ha uma coexistencia de nomes:

- repositorio: `Ajudante`
- varios namespaces/pastas: `Ajudante.*`
- nome do executavel e data dir: `Sidekick`

Exemplos:

- `AssemblyName` do app: `Sidekick`
- `%AppData%\Sidekick`
- mensagens de erro e startup usam `Sidekick`

Esse detalhe precisa ser levado em conta em qualquer trabalho de documentacao, empacotamento, telemetria ou refactor.

## Frente Aprovada Atualmente

A frente atual aprovada pelo usuario nao e mais runtime/branding, e sim `fortalecimento do produto`.

Documento principal dessa fase:

- `RFC_PRODUCT_STRENGTHENING.md`

Prioridade de execucao aprovada:

1. biblioteca de ativos do `Snip`
2. biblioteca de inspecoes do `Mira`
3. jornada guiada de criacao de flows
4. catalogo local de pacotes
5. marketplace remoto

Regra de continuidade:

- sempre registrar avancos reais em `PROJECT_STATUS.md`
- nao abrir fases posteriores sem fechar contratos minimos das anteriores

## Interface Do Usuario

A interface se organiza em:

- toolbar
- node palette
- canvas
- property panel
- status bar

Ela oferece:

- criar flow
- salvar
- carregar
- rodar/parar
- abrir Mira
- abrir Snip
- visualizar logs e status

O estado do flow e mantido em `zustand`, com dirty tracking por snapshot serializado.

Arquivo-chave:

- `src/Ajudante.UI/src/store/flowStore.ts`

## Mira E Snip

O projeto possui duas ferramentas auxiliares importantes:

- `Mira`: inspecao visual de elementos
- `Snip`: captura de regiao da tela

Essas funcoes sao disparadas pela bridge em `platform/*` e abrem janelas overlay no host WPF.
Depois da captura, os ativos do `Snip` podem ser persistidos e consultados pelo canal `assets/*`.
O editor tambem ja consegue reutilizar esse ativo em `trigger.imageDetected` por meio de um payload estruturado de `ImageTemplate`.

Arquivos relevantes:

- `src/Ajudante.App/Overlays/MiraWindow.xaml`
- `src/Ajudante.App/Overlays/SnipWindow.xaml`
- `src/Ajudante.App/Bridge/BridgeRouter.cs`

## Fluxos Demo E Portfolio

Os flows na pasta `flows/` sao importantes por tres motivos:

1. servem como demos para o usuario
2. entram no pacote publicado como `seed-flows`
3. funcionam como contrato pratico do formato esperado pelo sistema

Os testes tambem validam esses flows.

Arquivos relevantes:

- `tests/Ajudante.Core.Tests/SampleFlowsTests.cs`
- `flows/portfolio_snip_reuse_demo.json`

Esses testes garantem:

- ids unicos
- referencias de nos e conexoes validas
- round-trip estavel de serializacao
- presenca de triggers continuos
- exemplo estruturado de reuse de ativo do `Snip`

## Pipeline De Build E Publish

O app desktop depende do frontend compilado.

O `.csproj` do app contem logica para:

- validar presenca do projeto frontend
- validar `package.json`
- restaurar dependencias com `npm ci` quando necessario
- rodar `npm run build`
- sincronizar `wwwroot` para a saida publicada
- impedir publish inconsistente

Arquivo-chave:

- `src/Ajudante.App/Ajudante.App.csproj`

Isso e importante porque uma falha frequente neste tipo de arquitetura hibrida e publicar o app .NET com assets web incompletos ou desatualizados.

## Testes E Cobertura

Existem testes relevantes para:

- `FlowExecutor`
- `FlowSerializer`
- `FlowValidator`
- `NodeRegistry`
- sample flows
- nos de logica e acao
- bridge e parsing de mensagens

Projetos:

- `tests/Ajudante.Core.Tests`
- `tests/Ajudante.Nodes.Tests`

Esses testes servem como guia forte para manutencao segura.

## Pontos De Atencao Tecnicos

### 1. Threading Entre Executor, UI E Tray

O projeto depende fortemente de sincronizacao correta entre:

- thread de execucao do flow
- dispatcher WPF
- WebView2
- tray icon

Mudancas em eventos, notificacoes e bridge devem ser feitas com cautela.

### 2. Contrato Da Bridge

A bridge e um ponto sensivel do sistema.

Qualquer alteracao em:

- formato do payload
- nomes de `channel/action`
- serializacao JSON
- direcao dos eventos

pode quebrar a UI ou a execucao do desktop.

### 3. Integracao De Triggers Continuos

O `TriggerManager` existe, mas a integracao aparente com o host principal nao esta completa.

Um agente que queira evoluir o produto para automacao verdadeiramente continua provavelmente precisara:

- integrar ativacao/desativacao de triggers no ciclo do app
- definir UX clara para flows monitorados
- revisar relacao entre `IsRunning`, trigger mode e execucao pontual

### 4. Grafo E Ciclos

O executor percorre o flow de forma relativamente direta.

Qualquer evolucao em:

- loops complexos
- ciclos arbitrarios
- paralelismo

precisa ser analisada com cuidado para evitar recursao indevida ou comportamento inesperado.

### 5. Coexistencia Ajudante/Sidekick

Refactors, scripts e documentacao devem considerar que os dois nomes coexistem no projeto.

## Melhor Ponto De Entrada Para Um Novo Agente

Se outro agente precisar entrar rapidamente no projeto, a ordem recomendada de leitura e:

1. `README.md`
2. `USER_GUIDE.md`
3. `src/Ajudante.App/MainWindow.xaml.cs`
4. `src/Ajudante.App/Bridge/WebBridge.cs`
5. `src/Ajudante.App/Bridge/BridgeRouter.cs`
6. `src/Ajudante.Core/Engine/FlowExecutor.cs`
7. `src/Ajudante.Core/Registry/NodeRegistry.cs`
8. `src/Ajudante.UI/src/App.tsx`
9. `src/Ajudante.UI/src/store/flowStore.ts`
10. `src/Ajudante.UI/src/bridge/flowConverter.ts`
11. `tests/Ajudante.Core.Tests/SampleFlowsTests.cs`

## Resumo Executivo

O projeto e um editor visual de automacao Windows com runtime local, baseado em fluxos em grafo.

Seu centro tecnico esta em tres contratos:

1. `NodeDefinition` como metadado do no
2. `Flow` como formato persistido
3. `BridgeMessage` como protocolo entre UI e host desktop

Se esses tres contratos forem compreendidos, o restante do sistema fica muito mais facil de navegar.

No estado atual, o produto ja oferece:

- uma base funcional de automacao visual
- extensibilidade por plugins
- demos e testes relevantes
- pipeline de publish endurecido

As areas com maior potencial de evolucao parecem ser:

- integracao completa de triggers continuos
- refinamento de UX
- consolidacao de branding
- amplificacao do catalogo de nos e cenarios de automacao
