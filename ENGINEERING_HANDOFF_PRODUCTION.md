# ENGINEERING_HANDOFF_PRODUCTION.md

> Ordem obrigatoria de leitura antes de executar este handoff:
> `AGENTS.md` -> `PROJECT_STATUS.md` -> `RFC_PRODUCT_STRENGTHENING.md` -> este documento.
>
> Este handoff nao substitui `PROJECT_STATUS.md`. Ele operacionaliza a entrada do projeto em estagio de producao.

## Status

- Estado: `aprovado para execucao`
- Data de consolidacao: `2026-04-28`
- Escopo principal: `levar o Sidekick de beta tecnico para estagio de producao`
- Caso de prova obrigatorio: `macro desktop robusta para Trae.exe clicar em Continue quando surgir o aviso de thinking limit`

Atualizacao `2026-04-29`:

- Estado apos execucao: `release candidate tecnico`, com publish RC gerado em `src/Ajudante.App/bin/publish-rc`.
- O publish no caminho oficial `src/Ajudante.App/bin/publish` ficou bloqueado por uma instancia ja aberta de `Sidekick`; nao substituir sem fechar a instancia em execucao.
- Build/testes verificados: `.NET 245 testes`, `UI 24 testes`, `npm run build`, `dotnet build --no-restore`, `dotnet publish --no-restore`.
- Restore NuGet completo (`dotnet build/test` sem `--no-restore`) falhou no ambiente da sessao antes de compilar com `Value cannot be null. (Parameter 'path1')`.
- OCR ainda e limitacao residual; fallback visual entregue e `action.clickImageMatch` por template/image matching.

Atualizacao `2026-04-30`:

- Publish oficial em `src/Ajudante.App/bin/publish` foi revalidado apos fechar instancia que segurava DLLs.
- Canvas recebeu conexao assistida mais tolerante: soltar fio sem alvo abre `Adicionar passo`, e o raio de conexao foi ampliado para facilitar comportamento magnetico.
- Marketplace local de recipes oficiais esta visivel na toolbar.
- Nodes de overlay, console/PWD e hardware/sistema foram adicionados.
- Hardware/sistema inclui audio, mute de microfone, dispositivos PnP de camera/microfone/Wi-Fi, energia e display; mudancas de sistema usam `allowSystemChanges` ou frase `CONFIRM`.
- Validacao mais recente antes do publish final desta rodada: `.NET 250 testes`, `UI 37 testes`, `npm run build`, `dotnet build`.
- Entregas P0 adicionais de captura/gravao:
  - `action.captureScreenshot`
  - `action.recordDesktop`
  - `action.recordCamera`
- Entrega P0 adicional de logica:
  - `logic.conditionGroup` com `ANY/ALL`, nested groups simples e operadores `equals/contains/regex/greater/less/exists/changed`.
- Mira antiquebra ampliado no contrato (`ElementInfo` + payload bridge + prefill) para fallback selector -> relative -> scaled -> absolute.
- Recipes oficiais adicionados:
  - `recipe_screenshot_window_support.json`
  - `recipe_desktop_recording.json`
  - `recipe_camera_recording.json`
  - `recipe_mira_resilient_click.json`
  - `recipe_whatsapp_status_assistant.json`
- Regra de seguranca para WhatsApp reforcada: draft por padrao, envio apenas com consentimento explicito (`allowSendSensitiveData=true` + `sendMode=sendAfterConfirm`).
- Limitacao honesta desta rodada: gravacao de audio (sistema/camera) nao implementada.

Atualizacao `2026-04-30` (editor visual P0 e publish):

- Editor visual recebeu operacoes de produto para construcao sem sidebar: soltar fio no vazio para adicionar proximo passo, menu de contexto no canvas/node/edge, busca rapida, duplicar node, habilitar/desabilitar node, inserir node em edge, reconectar/remover edge e auto layout.
- Store/conversor cobertos por testes para criacao, conexao, reconexao, insercao, remocao, disabled metadata e runtime bypass de node desabilitado.
- `Run Now`, `Arm` e validacao pre-run usam `runtimeView`, desviando nodes desabilitados quando o grafo permite.
- Categorias publicas no palette agora refletem areas de produto: Trigger, Desktop, Window, Hardware, Media, Console, Logic, Data e Utility.
- Gates executados com sucesso nesta rodada: `dotnet build Ajudante.sln`, `dotnet test Ajudante.sln` (`257` testes), `npm run test` (`49` testes), `npm run build`, `dotnet publish` oficial para `src/Ajudante.App/bin/publish`.
- `Sidekick.exe` publicado existe; `wwwroot` publicado contem assets atuais; recipes oficiais foram publicados em `seed-flows/`.
- Validacao manual interativa do exe publicado ainda esta pendente; nao declarar RC final de distribuicao sem abrir o app e validar fluxo de canvas/Mira/Snip/Marketplace no WebView2.

## Resumo Executivo

O projeto ja possui base tecnica forte:

- editor visual funcional
- runtime manual e continuo integrados
- bridge desktop/web consistente
- `Snip` persistido com reuse basico
- `Mira` funcional com binding em nodes de seletor
- migracao `Ajudante` -> `Sidekick` operacional
- build e testes verdes no estado atual

Mas ainda nao deve ser tratado como produto de producao por cinco lacunas principais:

1. automacao desktop moderna ja possui trilha nativa, mas ainda precisa validacao manual ampla em apps reais
2. `Mira` ja gera inspecao rica e biblioteca reutilizavel; continuar refinando UX e dados de hierarquia
3. existe trilha nativa para `elemento apareceu` com cooldown, debounce e max repeat
4. observabilidade de fases foi adicionada, mas ainda falta screenshot on failure e export completo de suporte
5. ainda nao existe gate E2E automatizado contra apps alvo reais; o flow Trae tem prova estrutural e ainda depende de validacao no app real

## Avaliacao Do Estagio Atual

Classificacao proposta:

- `runtime e arquitetura`: `fortes`
- `automacao desktop moderna`: `promissora, mas incompleta`
- `UX de construcao de macros`: `fortalecida no editor visual, ainda pendente de validacao manual e onboarding completo`
- `operacao e suporte`: `abaixo de producao`
- `release readiness`: `quase pronta tecnicamente, ainda nao pronta como produto`

Conclusao pratica:

- o Sidekick esta em `beta tecnico / early beta`
- nao deve ser vendido internamente como "pronto para qualquer macro desktop"
- ja pode ser endurecido para producao se esta frente for executada integralmente

## Caso De Prova Obrigatorio: Trae Auto Continue

Objetivo funcional:

- quando o app alvo for `Trae.exe`
- localizado em `%LOCALAPPDATA%\\Programs\\Trae\\Trae.exe` ou caminho equivalente configurado pelo usuario
- e aparecer a mensagem:
  - `Model thinking limit reached, please enter 'Continue' to get more. Continue`
- o Sidekick deve clicar em `Continue`

Interpretacao tecnica honesta do estado atual:

- o flow oficial `flows/trae_auto_continue.json` usa `trigger.desktopElementAppeared`, `action.windowControl` e `action.desktopClickElement`
- o trigger esta filtrado por `processName`, `processPath`, `windowTitle contains`, `elementName=Continue`, `controlType=button`, `cooldownMs`, `debounceMs` e `maxRepeat`
- nao usa `manualStart` como entrega do caso
- se UIAutomation nao expuser o botao `Continue`, a trilha oficial de fallback e recapturar alvo com `Snip` e substituir/ramificar para `action.clickImageMatch`
- validacao manual no `Trae.exe` real ainda nao foi executada nesta sessao

Esse caso deve virar:

- sample flow oficial
- teste E2E oficial
- benchmark de readiness do produto

## Meta De Producao

O Sidekick so deve ser considerado em estagio de producao quando todos os criterios abaixo forem verdadeiros:

1. suporta automacao desktop moderna com UIAutomation e fallback visual de forma previsivel
2. possui runtime resiliente e observavel para execucao continua de longa duracao
3. possui UX suficiente para criar automacoes sem depender de conhecimento interno do repositorio
4. possui validacao automatizada, matriz de compatibilidade e checklist de release
5. possui caminhos formais de diagnostico, export e suporte

## Principios Obrigatorios Desta Fase

- honestidade funcional: nao prometer IA, OCR ou robustez que nao existam de fato
- desktop first: priorizar automacao de aplicativos Windows reais, nao apenas demos
- previsibilidade antes de amplitude: preferir poucos cenarios muito robustos a muitos cenarios fragis
- contratos explicitos: toda evolucao de node, bridge ou manifesto deve ter schema claro
- compatibilidade forte: nao quebrar flows, assets ou `%AppData%\\Sidekick`
- rollback simples: cada marco deve ser pequeno, testavel e reversivel
- testes de produto: toda capacidade critica deve ter prova manual e automatizada

## Frentes De Execucao

## Frente 1. Desktop Automation Runtime

### Objetivo

Transformar a automacao desktop moderna em capacidade nativa do produto.

### Entregas obrigatorias

1. criar um contrato desktop explicito, separado semanticamente dos atuais nodes `Browser*`
2. introduzir novos nodes desktop para espera, leitura, clique, foco e detecao de elemento
3. suportar seletores baseados em processo, janela, elemento e fallback visual
4. suportar regras de cooldown, debounce e repeat guard
5. transformar o caso Trae em fluxo oficial e teste oficial

### Capacidade minima a implementar

Novos triggers:

- `trigger.desktopElementAppeared`
- `trigger.desktopElementTextChanged`

Novas actions:

- `action.desktopWaitElement`
- `action.desktopClickElement`
- `action.desktopReadElementText`
- `action.focusWindow`
- `action.bringProcessWindowToFront`
- `action.clickImageMatch`

Compatibilidade:

- manter `BrowserWaitElement`, `BrowserClick`, `BrowserType`, `BrowserExtractText`
- expor aliases visuais ou estrategia de migracao progressiva
- nao quebrar flows existentes

### Filtros obrigatorios

- `processName`
- `processPath`
- `windowTitle`
- `windowTitleMatchMode` (`equals`, `contains`)
- `automationId`
- `elementName`
- `controlType`
- `timeoutMs`
- `cooldownMs`
- `debounceMs`
- `maxRepeatsPerWindow`

### Mudancas provaveis por arquivo/camada

Core / nodes / platform:

- `src/Ajudante.Platform/UIAutomation/AutomationElementLocator.cs`
- `src/Ajudante.Platform/UIAutomation/ElementInspector.cs`
- `src/Ajudante.Platform/Windows/*`
- `src/Ajudante.Nodes/Actions/Browser*.cs`
- `src/Ajudante.Nodes/Triggers/*`
- novas classes em `src/Ajudante.Nodes/Actions/`
- novas classes em `src/Ajudante.Nodes/Triggers/`

Bridge / app:

- `src/Ajudante.App/Bridge/BridgeRouter.cs`
- `src/Ajudante.App/Runtime/FlowRuntimeManager.cs`

Frontend:

- `src/Ajudante.UI/src/bridge/types.ts`
- `src/Ajudante.UI/src/components/Sidebar/PropertyPanel.tsx`
- `src/Ajudante.UI/src/store/*`

### Criterio de aceite

- o produto consegue detectar e clicar em `Continue` no Trae de forma repetivel
- o clique nao entra em loop
- o runtime nao trava a UI
- a ausencia do elemento nao derruba o flow inteiro sem diagnostico claro

## Frente 2. Mira Inspection Library 2.0

### Objetivo

Fazer o `Mira` sair de "captura transiente" e virar inspecao reutilizavel orientada a macro.

### Entregas obrigatorias

1. persistir inspecoes capturadas como ativos
2. exibir biblioteca de ativos do `Mira`
3. registrar contexto suficiente para automacao robusta
4. ranquear robustez do seletor
5. recomendar estrategia de localizacao

### Dados minimos por ativo

- `id`
- `version`
- `createdAt`
- `updatedAt`
- `displayName`
- `notes`
- `tags`
- `source.processName`
- `source.processPath`
- `source.windowTitle`
- `source.windowClassName`
- `selector.windowTitle`
- `selector.automationId`
- `selector.name`
- `selector.controlType`
- `selector.className`
- `bounds.absolute`
- `bounds.relativeToWindow`
- `recommendedStrategy`
- `selectorStrength`

### Regras de estrategia

Ordem de preferencia:

1. `selectorPreferred`
2. `relativePositionFallback`
3. `absolutePositionLastResort`

### UX obrigatoria

- `Use Latest`
- `Browse Mira`
- `Test Selector`
- `Show Match Strength`
- `Clear Binding`

### Criterio de aceite

- o usuario consegue capturar um elemento do Trae e reutiliza-lo em um flow sem JSON manual
- o editor comunica claramente quando o seletor e forte ou fraco

## Frente 3. Fallback Visual e OCR

### Objetivo

Cobrir apps que nao expoem bem UIAutomation.

### Entregas obrigatorias

1. OCR em regiao salva ou capturada
2. leitura textual de regiao
3. clique por match de imagem
4. pipeline formal de fallback: `invoke -> bounds click -> image match`

### Escopo minimo

- OCR local com edicao manual do texto persistido
- `action.captureElementRegion`
- `action.readRegionText`
- `action.clickImageMatch`
- suporte para `Snip` como asset de fallback

### Criterio de aceite

- se UIAutomation falhar no Trae, o produto ainda oferece estrategia suportada e documentada para resolver o caso

## Frente 4. Runtime Hardening

### Objetivo

Levar o runtime continuo a comportamento operacional de producao.

### Entregas obrigatorias

1. politicas de concorrencia por flow
2. timeout e retry por node
3. cooldown e guardas contra loops
4. health snapshot do runtime
5. opcao futura de restore de arm state, sem misturar estado estrutural com runtime por acidente

### Politicas obrigatorias

Por flow:

- `drop`
- `coalesce`
- `queueLatest`
- `queueAll`

Por node:

- `timeoutMs`
- `retryCount`
- `retryDelayMs`
- `retryBackoffMode`

### Mudancas provaveis

- `src/Ajudante.App/Runtime/FlowRuntimeManager.cs`
- `src/Ajudante.Core/Engine/FlowExecutor.cs`
- `src/Ajudante.Core/Models/*`
- `src/Ajudante.UI/src/bridge/types.ts`
- `src/Ajudante.UI/src/store/appStore.ts`

### Criterio de aceite

- o app pode permanecer armado por longos periodos sem degradação visivel
- triggers em burst continuam previsiveis
- loops acidentais ficam contidos por politica e log

## Frente 5. Observabilidade e Diagnostico

### Objetivo

Permitir suporte real, depuracao rapida e confianca operacional.

### Entregas obrigatorias

1. logs estruturados
2. timeline de execucao por flow
3. painel de diagnostico
4. export de pacote de suporte

### Campos de log minimos

- `flowId`
- `flowName`
- `runId`
- `triggerId`
- `source`
- `nodeId`
- `eventType`
- `durationMs`
- `errorCode`
- `errorMessage`
- `selectorSummary`

### UX minima

- listar flows armados
- mostrar fila
- mostrar ultimo erro por flow
- mostrar trigger mais recente
- exportar logs + flow + assets referenciados

### Criterio de aceite

- um bug report do caso Trae pode ser exportado com contexto suficiente para reproducao

## Frente 6. Jornada Guiada de Fluxos

### Objetivo

Reduzir drasticamente a friccao entre capturar algo e criar uma macro funcional.

### Entregas obrigatorias

1. templates de fluxo guiados
2. wizard de `desktop automation`
3. presets honestos para casos comuns
4. validacao pre-armar

### Recipes obrigatorios

- `Trae auto-continue`
- `desktop popup auto-confirm`
- `wait text then click`
- `hotkey starts desktop action`

### Criterio de aceite

- um usuario tecnico nao maintainer monta o caso Trae em poucos minutos usando recipe ou wizard

## Frente 7. Assets, Pacotes e Governanca

### Objetivo

Preparar o produto para compartilhamento e reaproveitamento com controle.

### Entregas obrigatorias

1. catalogo local de assets do `Mira`
2. empacotamento local de flow + assets dependentes
3. validacao de compatibilidade
4. rollback seguro de import

### Tipos de pacote

- `flow-pack`
- `asset-pack`
- `integration-pack`

### Criterio de aceite

- o flow do Trae pode ser exportado com seus assets e reimportado em outra maquina sem perda silenciosa

## Frente 8. QA, Compatibilidade e Release Engineering

### Objetivo

Definir e fechar o gate de producao.

### Entregas obrigatorias

1. testes E2E desktop reais
2. matriz de compatibilidade
3. soak tests
4. checklist de release
5. validacao de instalacao e upgrade

### Matriz minima

- Windows 10
- Windows 11
- DPI 100 / 125 / 150
- single monitor
- multi-monitor
- light / dark theme
- app alvo Electron
- app alvo Win32
- app alvo WPF

### Soak tests minimos

- 8h armado
- 24h armado
- burst de trigger
- abrir e fechar app alvo repetidamente

### Criterio de aceite

- o caso Trae passa em pelo menos um ambiente-alvo suportado e fica documentado como `validated`

## Roadmap Obrigatorio De Implementacao

### Marco A. Desktop Automation First

Entregar:

- novos contracts desktop
- seletor robusto com `processPath`
- `Mira` enriquecido
- flow oficial do Trae
- teste E2E do Trae

DoD:

- o caso Trae funciona de forma repetivel em ambiente de desenvolvimento real

### Marco B. Reliable Runtime

Entregar:

- cooldown
- debounce
- timeout
- retry
- politicas de concorrencia
- health snapshot

DoD:

- runtime continuo aguenta bursts e long runs sem comportamento opaco

### Marco C. Operator UX

Entregar:

- biblioteca Mira
- timeline de execucao
- painel de diagnostico
- recipes guiados
- validacao pre-armar

DoD:

- o produto explica melhor o que esta acontecendo e reduz tentativa-e-erro

### Marco D. Production Gate

Entregar:

- pacote local confiavel
- release checklist
- compatibilidade documentada
- suporte exportavel
- validacao final do executavel publicado

DoD:

- o projeto pode ser tratado como pronto para producao controlada

## Backlog Prioritario Inicial

### P0

- adicionar suporte a `processPath` na trilha de localizacao desktop
- criar `trigger.desktopElementAppeared`
- criar `action.desktopClickElement`
- criar `action.desktopReadElementText`
- persistir ativos do `Mira`
- criar sample flow oficial do Trae
- criar teste E2E do Trae

### P1

- OCR de regiao
- `action.clickImageMatch`
- selector strength score
- timeline de execucao
- export de pacote de suporte
- recipe builder para desktop automation

### P2

- restore opcional de arm state
- package format local
- import/export de flow-pack
- matriz de compatibilidade automatizada parcial

## Riscos Principais

### Risco 1. Electron / apps modernos exporem arvore de acessibilidade inconsistente

Mitigacao:

- manter fallback visual oficial
- registrar `selectorStrength`
- nunca prometer universalidade sem fallback

### Risco 2. Crescimento desordenado de nodes

Mitigacao:

- contratos desktop explicitos
- aliases em vez de proliferacao semantica
- docs e recipes alinhados

### Risco 3. Loops de clique acidentais

Mitigacao:

- cooldown
- debounce
- max repeats por janela
- logs explicitos

### Risco 4. UX forte no editor, mas fraca no executavel publicado

Mitigacao:

- sempre validar `dotnet publish`
- testar o binario final usado pelo usuario

### Risco 5. Planejamento sem fechamento operacional

Mitigacao:

- cada marco deve atualizar `PROJECT_STATUS.md`
- cada marco deve fechar testes e criterio de aceite
- nenhum marco e considerado concluido apenas por merge visual

## Validacao Obrigatoria Por Marco

Sempre executar, no minimo:

```powershell
dotnet build Ajudante.sln
dotnet test Ajudante.sln
cd src/Ajudante.UI
npm run test
npm run build
```

Para marcos que afetam o executavel:

```powershell
dotnet publish .\\src\\Ajudante.App\\Ajudante.App.csproj -c Release -o .\\src\\Ajudante.App\\bin\\publish
```

Para marcos que afetam automacao desktop:

- validacao manual no executavel publicado
- validacao manual no app alvo real
- registro do comportamento observado em `PROJECT_STATUS.md`

## Definicao Final De Producao

So considerar o Sidekick em `estagio de producao` quando:

- o caso Trae estiver suportado oficialmente
- houver UIAutomation desktop robusto com fallback visual suportado
- o runtime continuo estiver endurecido com politicas explicitas
- a observabilidade estiver suficiente para suporte real
- os recipes principais estiverem prontos
- a matriz minima de compatibilidade estiver executada
- o executavel publicado tiver sido validado no fluxo real

## Protocolo Obrigatorio De Atualizacao Documental

Ao fim de cada marco, o agente executor deve atualizar:

1. `PROJECT_STATUS.md`
2. `RFC_PRODUCT_STRENGTHENING.md`
3. `AGENT_HANDOFF.md`, se houver novo contexto operacional importante
4. este documento, se backlog, ordem ou criterio de aceite mudarem

## Prompt Para Agente Executor

Voce esta assumindo a proxima fase do projeto `Ajudante` / `Sidekick` com objetivo explicito de levar o produto a estagio de producao controlada.

Leia obrigatoriamente nesta ordem:

1. `AGENTS.md`
2. `PROJECT_STATUS.md`
3. `RFC_PRODUCT_STRENGTHENING.md`
4. `ENGINEERING_HANDOFF_PRODUCTION.md`
5. `README.md`
6. `USER_GUIDE.md`

Depois aprofunde especialmente nestes arquivos:

- `src/Ajudante.App/Runtime/FlowRuntimeManager.cs`
- `src/Ajudante.App/Bridge/BridgeRouter.cs`
- `src/Ajudante.App/Overlays/MiraWindow.xaml.cs`
- `src/Ajudante.Core/Engine/FlowExecutor.cs`
- `src/Ajudante.Core/Registry/NodeRegistry.cs`
- `src/Ajudante.Platform/UIAutomation/AutomationElementLocator.cs`
- `src/Ajudante.Platform/UIAutomation/ElementInspector.cs`
- `src/Ajudante.Nodes/Actions/BrowserClickNode.cs`
- `src/Ajudante.Nodes/Actions/BrowserWaitElementNode.cs`
- `src/Ajudante.Nodes/Actions/BrowserExtractTextNode.cs`
- `src/Ajudante.Nodes/Actions/OpenProgramNode.cs`
- `src/Ajudante.Nodes/Triggers/WindowEventTriggerNode.cs`
- `src/Ajudante.UI/src/App.tsx`
- `src/Ajudante.UI/src/bridge/types.ts`
- `src/Ajudante.UI/src/components/Sidebar/PropertyPanel.tsx`
- `src/Ajudante.UI/src/store/appStore.ts`
- `src/Ajudante.UI/src/store/flowStore.ts`
- `tests/Ajudante.Core.Tests/*`
- `tests/Ajudante.Nodes.Tests/*`
- `flows/*.json`

Sua missao principal e executar o plano de producao descrito em `ENGINEERING_HANDOFF_PRODUCTION.md`, com prioridade inicial obrigatoria em:

1. Desktop Automation First
2. caso oficial `Trae auto-continue`
3. persistencia e biblioteca do `Mira`
4. runtime hardening necessario para evitar loops e comportamentos opacos

Entregas minimas desta primeira rodada:

- suporte a filtros desktop por `processName` e `processPath`
- trilha robusta para detectar elemento desktop e clicar nele
- caso de uso do Trae suportado por flow oficial
- pelo menos um teste automatizado cobrindo a trilha principal
- documentacao e status atualizados

Regras obrigatorias de execucao:

- nao faca rename massivo de namespaces ou projetos
- preserve compatibilidade com flows e assets existentes
- nao misture estado estrutural do flow com estado efemero de runtime sem justificativa forte
- qualquer mudanca de bridge exige atualizacao de tipos TS e testes correspondentes
- qualquer novo manifesto ou formato persistido precisa de `version` e `schemaVersion` quando aplicavel
- qualquer nova automacao precisa de fallback honesto ou mensagem clara de limitacao
- qualquer capacidade afirmada como pronta precisa ter validacao automatizada e, quando aplicavel, validacao manual no executavel publicado

Sequencia de trabalho esperada:

1. diagnosticar lacunas exatas entre o handoff e o codigo atual
2. produzir plano de execucao curto e priorizado
3. implementar primeiro a trilha desktop para o caso Trae
4. endurecer runtime e UX necessarios para tornar essa trilha confiavel
5. adicionar testes
6. rodar build, testes e publish
7. validar no executavel final
8. atualizar `PROJECT_STATUS.md` e docs
9. resumir o estado final, o que ficou pronto e os riscos residuais

Comandos minimos de validacao antes de encerrar:

```powershell
dotnet build Ajudante.sln
dotnet test Ajudante.sln
cd src/Ajudante.UI
npm run test
npm run build
cd ..\\..
dotnet publish .\\src\\Ajudante.App\\Ajudante.App.csproj -c Release -o .\\src\\Ajudante.App\\bin\\publish
```

Seu comportamento esperado e de dono tecnico da entrega:

- ser criterioso
- ser proativo
- fechar ponta solta
- atualizar documentos de estado
- nao parar em analise quando houver implementacao clara a fazer
