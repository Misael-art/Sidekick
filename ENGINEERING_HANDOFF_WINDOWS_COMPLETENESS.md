# ENGINEERING_HANDOFF_WINDOWS_COMPLETENESS.md

> Ordem obrigatoria de leitura antes de executar este handoff:
> `AGENTS.md` -> `PROJECT_STATUS.md` -> `RFC_PRODUCT_STRENGTHENING.md` -> `ENGINEERING_HANDOFF_PRODUCTION.md` -> `WINDOWS_AUTOMATION_COMPLETENESS_MATRIX.md` -> este documento.

## Status

- Estado: `executado parcialmente para release candidate tecnico`
- Data de consolidacao: `2026-04-29`
- Escopo: `fechar a cobertura Windows do Sidekick com criterio de produto, nao apenas de arquitetura`

Resultado da rodada `2026-04-29`:

- Bloco 1 concluido para RC: `Mira` rico, manifesto expandido, biblioteca, busca, preview, aplicar ao node, apagar e `Test Selector`.
- Bloco 2 concluido para RC: selectors desktop com `processName`, `processPath`, `windowTitleMatch`; nodes `desktopWaitElement`, `desktopClickElement`, `desktopReadElementText`, `desktopElementAppeared`, `desktopElementTextChanged`.
- Bloco 3 parcial: `action.clickImageMatch` e mensagens de fallback visual entregues; OCR funcional nao foi entregue nesta rodada.
- Bloco 4 concluido para RC: `action.windowControl`, `action.waitProcess` e `trigger.processEvent`.
- Bloco 5 concluido para RC: `trigger.scheduleTime` e `trigger.interval`.
- Bloco 6 parcial/concluido para RC: recipes oficiais adicionados em `flows/`; wizard dedicado ainda nao existe.
- Bloco 7 concluido para RC tecnico: fases semanticas do runtime, logs de fase, erro de ciclo/step budget e historico mais claro.
- Bloco de publish: publish oficial em `src/Ajudante.App/bin/publish` foi revalidado apos fechar a instancia que bloqueava DLLs.
- Canvas: conexao assistida abre `Adicionar passo` quando o usuario solta fio sem alvo, com raio de conexao maior para comportamento magnetico.
- Hardware/sistema: nodes publicos para audio, microfone, camera, Wi-Fi, energia e display foram adicionados com guardas explicitas.

## Objetivo

Executar a matriz `WINDOWS_AUTOMATION_COMPLETENESS_MATRIX.md` ate o ponto em que o produto deixe de ser `beta tecnico` e passe a suportar automacao Windows real em cenarios variados.

Este handoff existe para evitar dois erros recorrentes:

1. implementar muita base tecnica sem entregar experiencia de produto
2. declarar capacidades como prontas sem validar no executavel publicado

## Regra De Ouro

Nao promova uma capacidade de `parcial` para `existe` sem fechar simultaneamente:

- backend
- bridge
- UX
- testes
- publish
- validacao manual quando aplicavel

## Missao Do Agente Executor

O agente executor deve entregar o Sidekick como plataforma Windows muito mais completa, com prioridade maxima para:

1. `Mira` de nivel produto
2. automacao desktop nativa baseada em seletor
3. fallback visual/OCR
4. acoes de janela e processo
5. scheduler e automacoes temporizadas
6. recipes reais para usuario final
7. observabilidade e suporte

## Escopo Obrigatorio Da Rodada

O agente deve fechar ao menos estes blocos como `existe` ou deixar prova tecnica objetiva do bloqueio:

### Bloco 1. Mira rico e biblioteca de inspecoes

- overlay com informacoes ricas
- persistencia de inspecoes
- biblioteca de inspecoes
- aplicar seletor ao node selecionado
- `Test Selector`
- score de robustez do seletor

### Bloco 2. Desktop selectors e automacao de elemento

- filtro por `processName`
- filtro por `processPath`
- `windowTitle` com `equals` e `contains`
- `action.desktopWaitElement`
- `action.desktopClickElement`
- `action.desktopReadElementText`
- `trigger.desktopElementAppeared`
- `trigger.desktopElementTextChanged`

### Bloco 3. Fallback visual/OCR

- `action.clickImageMatch`
- OCR inicial funcional
- pipeline `selector -> relative -> image -> absolute`
- mensagens honestas de fallback

Status `2026-04-29`: `action.clickImageMatch` e pipeline honesto entregues; OCR permanece limitacao residual e nao deve ser anunciado como pronto.

### Bloco 4. Janela, processo e shell

- `focus window`
- `bring to front`
- `minimize`
- `restore`
- trigger por processo start/stop ou justificativa tecnica para adiar
- base para dialogs/Explorer

### Bloco 5. Scheduler

- trigger de horario fixo
- trigger de intervalo
- janela de execucao minima, se couber sem risco excessivo

### Bloco 6. Recipes, exemplos e UX de criacao

- recipe `Trae auto-continue`
- recipe `wait text then click`
- recipe `popup auto-confirm`
- sample flows uteis de verdade
- validacao pre-armar

### Bloco 7. Observabilidade

- estados semanticos na UI
- timeline minima
- diagnostico de seletor/flow
- export de pacote de suporte minimo

## Arquivos Mais Provavelmente Afetados

### App / runtime / bridge

- `src/Ajudante.App/MainWindow.xaml.cs`
- `src/Ajudante.App/Bridge/BridgeRouter.cs`
- `src/Ajudante.App/Bridge/BridgeMessage.cs`
- `src/Ajudante.App/Overlays/MiraWindow.xaml`
- `src/Ajudante.App/Overlays/MiraWindow.xaml.cs`
- `src/Ajudante.App/Runtime/FlowRuntimeManager.cs`
- possivel nova pasta `src/Ajudante.App/Assets/` ou `src/Ajudante.App/Automation/`

### Platform

- `src/Ajudante.Platform/UIAutomation/ElementInfo.cs`
- `src/Ajudante.Platform/UIAutomation/ElementInspector.cs`
- `src/Ajudante.Platform/UIAutomation/AutomationElementLocator.cs`
- `src/Ajudante.Platform/Windows/*`
- possivel OCR helper novo em `src/Ajudante.Platform/Screen/` ou subpasta nova

### Nodes

- `src/Ajudante.Nodes/Actions/BrowserClickNode.cs`
- `src/Ajudante.Nodes/Actions/BrowserWaitElementNode.cs`
- `src/Ajudante.Nodes/Actions/BrowserExtractTextNode.cs`
- `src/Ajudante.Nodes/Actions/BrowserTypeNode.cs`
- novas actions em `src/Ajudante.Nodes/Actions/`
- novas triggers em `src/Ajudante.Nodes/Triggers/`
- possivel node de scheduling

### UI

- `src/Ajudante.UI/src/App.tsx`
- `src/Ajudante.UI/src/bridge/types.ts`
- `src/Ajudante.UI/src/bridge/bridge.ts`
- `src/Ajudante.UI/src/store/appStore.ts`
- `src/Ajudante.UI/src/store/flowStore.ts`
- `src/Ajudante.UI/src/components/Sidebar/PropertyPanel.tsx`
- `src/Ajudante.UI/src/components/Toolbar/Toolbar.tsx`
- `src/Ajudante.UI/src/components/StatusBar/ExecutionStatus.tsx`
- possivel nova area para `Mira Library`, `Recipes`, `Diagnostics`

### Tests

- `tests/Ajudante.Core.Tests/*`
- `tests/Ajudante.Nodes.Tests/*`
- `src/Ajudante.UI/src/**/*.test.ts*`

### Samples e docs

- `flows/*.json`
- `README.md`
- `USER_GUIDE.md`
- `PROJECT_STATUS.md`
- `RFC_PRODUCT_STRENGTHENING.md`
- `ENGINEERING_HANDOFF_PRODUCTION.md`
- `WINDOWS_AUTOMATION_COMPLETENESS_MATRIX.md`

## Sequencia Obrigatoria De Implementacao

### Fase 1. Fechar Mira e Desktop Selector Backbone

DoD:

- o `Mira` no exe publicado mostra dados suficientes para automacao real
- o usuario consegue salvar e reaplicar seletor
- `processPath` esta funcional na trilha inteira

### Fase 2. Fechar Trigger/Action de Elemento Desktop

DoD:

- usuario consegue esperar, ler e clicar em elemento desktop sem grafo excessivamente tecnico
- caso `Trae` ja pode ser montado com essa trilha

### Fase 3. Fechar Fallback Visual/OCR

DoD:

- quando selector falhar, o produto tem degradacao clara e suportada

### Fase 4. Fechar Window/Process/Scheduler

DoD:

- usuario consegue construir automacoes mistas orientadas a horario, janela e evento

### Fase 5. Fechar Recipes, Samples e UX

DoD:

- os principais fluxos deixam de parecer mocks e viram ativos reais do produto

### Fase 6. Fechar Observabilidade e Supportability

DoD:

- um usuario ou maintainer entende por que o flow rodou, esperou, falhou ou clicou

### Fase 7. Fechar Publish e Matriz de Compatibilidade

DoD:

- o executavel publicado foi validado nos cenarios Windows declarados

## Protocolo Para Reduzir Falhas

### Antes de cada subfrente

1. confirmar quais itens da matriz estao em `parcial` ou `falta`
2. localizar contratos de backend, bridge, UI e sample flows afetados
3. escrever plano curto da rodada
4. listar riscos de regressao

### Durante a implementacao

1. nao introduzir capacidade magica sem fallback ou limitacao clara
2. nao alterar bridge sem atualizar `types.ts` e testes
3. nao criar exemplo mockado e vender como recipe real
4. nao tratar publish como detalhe; validar sempre no exe final

### Antes de declarar concluido

1. atualizar docs
2. publicar novamente
3. validar manualmente no exe publicado
4. conferir a matriz e promover apenas o que foi realmente fechado

## Checklist De Validacao Obrigatoria

### Build e testes

```powershell
dotnet build Ajudante.sln
dotnet test Ajudante.sln
cd src/Ajudante.UI
npm run test
npm run build
cd ..\..
dotnet publish .\src\Ajudante.App\Ajudante.App.csproj -c Release -o .\src\Ajudante.App\bin\publish
```

### Validacao manual obrigatoria no publish

Executavel alvo:

- `F:\Projects\Ajudante\src\Ajudante.App\bin\publish\Sidekick.exe`

Checagens obrigatorias:

1. `Mira` mostra dados ricos no overlay
2. biblioteca de `Mira` aparece e funciona
3. seletor salvo pode ser aplicado a um node
4. recipes/samples novos sao realmente uteis
5. runtime mostra estados compreensiveis
6. caso `Trae auto-continue` funciona ou fica provado tecnicamente por que nao fecha

## Regras De Honestidade Funcional

Se encontrar bloqueio real de plataforma, Electron, permissao ou acessibilidade:

1. implemente o maximo possivel
2. adicione fallback
3. registre o bloqueio em `PROJECT_STATUS.md`
4. nao promova a capacidade a `existe`
5. ajuste samples, docs e prompt final para refletir o estado verdadeiro

## Prompt Para Outro Agente De IA

Voce esta assumindo a execucao completa da cobertura Windows do projeto `Ajudante` / `Sidekick`.

Leia obrigatoriamente nesta ordem:

1. `AGENTS.md`
2. `PROJECT_STATUS.md`
3. `RFC_PRODUCT_STRENGTHENING.md`
4. `ENGINEERING_HANDOFF_PRODUCTION.md`
5. `WINDOWS_AUTOMATION_COMPLETENESS_MATRIX.md`
6. `ENGINEERING_HANDOFF_WINDOWS_COMPLETENESS.md`
7. `README.md`
8. `USER_GUIDE.md`

Sua missao e fechar a matriz `WINDOWS_AUTOMATION_COMPLETENESS_MATRIX.md` com criterio de produto real para Windows.

Nao pare em arquitetura boa com UX fraca.
Nao pare em backend pronto sem publish validado.
Nao promova item de `parcial` para `existe` sem:

- backend
- bridge
- UI
- testes
- publish
- validacao manual

Prioridades obrigatorias:

1. `Mira` de produto com biblioteca de inspecoes
2. desktop selectors completos com `processPath`
3. trigger/action nativos de elemento desktop
4. fallback visual/OCR
5. acoes de janela/processo
6. scheduler minimo
7. recipes e samples reais
8. observabilidade e suporte

Entregas minimas obrigatorias:

- `Mira` mostra dados ricos e permite salvar/aplicar seletor
- biblioteca de `Mira` funcional
- `trigger.desktopElementAppeared`
- `action.desktopWaitElement`
- `action.desktopClickElement`
- `action.desktopReadElementText`
- `action.clickImageMatch`
- trigger de horario fixo e trigger de intervalo
- `focus`, `bring to front`, `minimize`, `restore`
- recipe e sample reais para `Trae auto-continue`
- runtime com estados semanticos visiveis
- docs atualizadas

Regras obrigatorias:

- preserve compatibilidade com flows existentes quando razoavel
- nao faca rename massivo de namespaces/projetos
- qualquer mudanca de bridge exige tipos TS e testes
- qualquer manifesto novo deve ter versao/schema
- qualquer nova automacao deve ter fallback honesto ou limitacao documentada
- qualquer claim de pronto exige validacao no exe publicado

Sequencia esperada:

1. diagnosticar no codigo e no publish os gaps ainda abertos da matriz
2. escolher um bloco P0 e fechar ponta a ponta
3. validar build/test/publish
4. validar manualmente
5. atualizar a matriz e docs
6. repetir para o proximo bloco P0

So encerre quando:

- a maior parte dos itens P0 da matriz estiver `existe`
- os itens restantes tiverem bloqueio tecnico documentado
- o executavel publicado refletir de verdade o estado documentado
