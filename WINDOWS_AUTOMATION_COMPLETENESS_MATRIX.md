# WINDOWS_AUTOMATION_COMPLETENESS_MATRIX.md

> Ordem obrigatoria de leitura antes de usar esta matriz:
> `AGENTS.md` -> `PROJECT_STATUS.md` -> `RFC_PRODUCT_STRENGTHENING.md` -> `ENGINEERING_HANDOFF_PRODUCTION.md` -> este documento.

## Status

- Estado: `release candidate tecnico parcial`
- Data de consolidacao: `2026-04-29`
- Objetivo: mapear tudo o que o Sidekick precisa ter para ser uma plataforma Windows realmente completa nas mais diversas situacoes
- Caso de prova principal: `automacoes desktop reais, multiplos eventos, multiplas janelas, fallback visual, publish validado`

## Como Ler Esta Matriz

### Status

- `existe`: capacidade ja esta implementada e utilizavel no produto
- `parcial`: existe base tecnica, mas ainda nao entrega experiencia ou robustez de produto
- `falta`: nao existe de forma nativa suficiente

### Prioridade

- `P0`: bloqueia casos reais e should ship
- `P1`: muito importante para produto confiavel
- `P2`: amplia cobertura e escala operacional

## Resumo Executivo

O Sidekick hoje ja possui:

- runtime manual e continuo
- grafo visual funcional
- nodes de mouse, teclado, arquivos, HTTP e alguns gatilhos
- trilha nativa de UIAutomation desktop com selectors por janela/processo/caminho
- `Snip` com persistencia parcial e fallback visual por image matching
- `Mira` rico com biblioteca de inspecoes, score, estrategia sugerida e `Test Selector`
- triggers de horario, intervalo, processo e elemento desktop
- actions de janela/processo e elemento desktop
- actions de overlay visual (`overlayColor`, `overlayImage`, `overlayText`) para mensagens, planos de tela e recursos visuais temporizados
- actions de console/PWD (`consoleSetDirectory`, `consoleCommand`) com timeout, stdout/stderr, exit code e porta de erro
- handles visiveis de conexao no canvas e Marketplace local de recipes oficiais na toolbar

O que ainda impede o produto de ser completo no Windows:

1. OCR ainda nao esta funcional como produto
2. automacao de Explorer/dialogs e menus ainda nao possui trilha dedicada
3. recorder de macro e screenshot on failure ainda faltam
4. validacao manual ampla em apps reais, incluindo `Trae.exe`, ainda precisa ser concluida

## Matriz Completa

## A. Runtime, Orquestracao e Controle De Fluxo

| Capacidade | Status | Realidade atual | Gap para produto | Prioridade |
|---|---|---|---|---|
| Execucao manual de flow | existe | `runFlow` e fluxo basico operam | manter | P0 |
| Runtime continuo com triggers | existe | `FlowRuntimeManager` ja arma e monitora | fortalecer UX e diagnostico | P0 |
| Diferenciar `armed` vs `running` | existe | estado ja separado no runtime/UI | ampliar clareza visual | P0 |
| Fila global de execucao | existe | fila e coalescencia ja existem | expor melhor politicas | P1 |
| Politica de concorrencia por flow | parcial | coalescencia existe, mas nao e configuravel | `drop`, `coalesce`, `queueLatest`, `queueAll` | P1 |
| Stop atual e stop total | existe | `currentOnly` e `cancelAll` existem | melhorar feedback visual | P1 |
| Pause/resume de flow | falta | nao ha controle nativo | adicionar estado e bridge | P1 |
| Timeout por node | falta | ainda nao e contrato de produto | expor no modelo e executor | P1 |
| Retry por node | parcial | existe `logic.retryFlow`, mas nao e politica nativa geral | retry padrao por acao | P1 |
| Cooldown por flow/trigger | parcial | `trigger.desktopElementAppeared`, `trigger.desktopElementTextChanged` e `logic.cooldown` cobrem casos P0 | politica global por flow ainda falta | P0 |
| Debounce por trigger/elemento | parcial | triggers desktop possuem debounce configuravel | generalizar para outras familias | P0 |
| Max repeat por janela/elemento | parcial | triggers desktop e interval possuem `maxRepeat` | politica global ainda falta | P0 |
| Stop on event | falta | precisa ser modelado por composicao fragil | criar capacidade nativa | P1 |
| Subflows/reusable flow calls | falta | nao ha subflow nativo | reduzir complexidade do grafo | P2 |
| Variaveis compostas (lista/objeto) | parcial | variaveis simples existem | arrays/objetos/colecoes | P2 |
| `try/catch/finally` visual | falta | tratamento de erro e indireto | node ou bloco nativo | P2 |
| Checkpoint/recovery de execucao | falta | nao ha persistencia de progresso | produto resiliente | P2 |
| Restore opcional de arm state | falta | ainda nao ha restore formal | retomar automacoes apos restart | P2 |

## B. Triggers De Tempo E Agenda

| Capacidade | Status | Realidade atual | Gap para produto | Prioridade |
|---|---|---|---|---|
| Trigger por horario fixo | existe | `trigger.scheduleTime` | validar UX recorrente em uso longo | P0 |
| Trigger por intervalo | existe | `trigger.interval` | validar UX recorrente em uso longo | P0 |
| Trigger por dias da semana | falta | nao existe node nativo | uso real corporativo | P1 |
| Janela de execucao (start/end) | falta | nao existe | controle operacional | P1 |
| Cron-like schedule | falta | nao existe | cobertura avancada | P2 |
| Delayed start | falta | nao existe como trigger | flows agendados | P1 |
| Expiracao de automacao | falta | nao existe | governanca | P2 |

## C. Mouse, Teclado e Macro De Entrada

| Capacidade | Status | Realidade atual | Gap para produto | Prioridade |
|---|---|---|---|---|
| Click por coordenada | existe | `action.mouseClick` | melhorar presets e recipes | P0 |
| Movimento de mouse | existe | `action.mouseMove` | melhorar workflow de captura | P1 |
| Drag and drop por coordenada | existe | `action.mouseDrag` | precisa UX melhor | P1 |
| Scroll wheel | parcial | base platform existe, node dedicado nao e central de produto | expor melhor | P2 |
| Double click / right click / middle | existe | ja coberto em mouse click | manter | P1 |
| Keyboard type | existe | `action.keyboardType` | melhorar integracao com elementos | P1 |
| Keyboard press / combo | existe | `action.keyboardPress` | manter | P1 |
| Clipboard read/write | existe | nodes ja existem | manter | P2 |
| Macro recorder de input | falta | nao existe | grande ganho para usuario final | P1 |
| Sequencia de passos temporizada | parcial | possivel via nodes separados | recipe/node nativo falta | P1 |
| Repeticao por etapa | falta | usuario precisa montar manualmente | wizard/node nativo | P1 |
| Coordenada relativa a janela | falta | foco atual e absoluto | reduzir fragilidade | P0 |
| Coordenada relativa a ancora visual | falta | nao existe nativamente | fallback visual robusto | P1 |

## D. Processo, Janela e Shell Do Windows

| Capacidade | Status | Realidade atual | Gap para produto | Prioridade |
|---|---|---|---|---|
| Abrir programa | existe | `action.openProgram` | manter | P0 |
| Fechar processo | existe | `action.killProcess` | manter | P1 |
| Esperar processo iniciar | existe | `action.waitProcess` com `eventType=started` | validar em apps reais | P1 |
| Esperar processo fechar | existe | `action.waitProcess` com `eventType=stopped` | validar em apps reais | P2 |
| Detectar processo start/stop | existe | `trigger.processEvent` | validar em apps reais | P1 |
| Trazer janela para frente | existe | `action.windowControl` com `bringToFront` | validar foco em janelas elevadas | P0 |
| Focar janela especifica | existe | `action.windowControl` com `focus` | validar foco em janelas elevadas | P0 |
| Minimizar janela | existe | `action.windowControl` com `minimize` | validar em apps reais | P0 |
| Maximizar janela | existe | `action.windowControl` com `maximize` | validar em apps reais | P1 |
| Restaurar janela | existe | `action.windowControl` com `restore` | validar em apps reais | P1 |
| Mover/redimensionar janela | falta | nao ha action nativa | casos operacionais | P2 |
| Fechar janela por seletor | falta | nao ha action nativa clara | util | P2 |
| Trigger de janela abrir/fechar/focar | existe | `trigger.windowEvent` ja cobre base | enriquecer filtros e UX | P0 |
| Trigger de minimizar/restaurar | falta | nao coberto | casos reais | P1 |
| Interagir com tray/system menu | falta | nao ha suporte de produto | shell Windows real | P2 |
| Definir PWD de automacao | existe | `action.consoleSetDirectory` define variavel de working directory | validar em workflows longos | P1 |
| Executar comando de console | existe | `action.consoleCommand` suporta `direct`, `cmd`, `powershell`, timeout e captura de saida | governanca para marketplace/importacao | P0 |
| Capturar stdout/stderr/exit code | existe | outputs e variaveis opcionais no `action.consoleCommand` | melhorar visualizacao dedicada | P1 |
| Politica de comandos perigosos | parcial | node existe, mas marketplace remoto ainda precisa aviso/assinatura | bloquear execucao automatica importada | P0 |

## E. UIAutomation E Elementos Desktop

| Capacidade | Status | Realidade atual | Gap para produto | Prioridade |
|---|---|---|---|---|
| Inspecionar elemento sob cursor | existe | `Mira` captura dados ricos e curados | refinar hierarquia/tree path | P0 |
| Capturar `processName` | existe | aparece no overlay e manifesto do `Mira` | manter | P0 |
| Capturar `processPath` | existe | aparece no overlay e manifesto do `Mira` | pode falhar em processos protegidos/elevados | P0 |
| Capturar `windowTitle` | existe | aparece no overlay e manifesto | manter | P0 |
| Capturar `automationId` | existe | aparece com score e teste de seletor | manter | P0 |
| Capturar `className` | existe | aparece no overlay/manifesto | manter | P1 |
| Capturar `controlType` | existe | aparece no overlay/manifesto | manter | P1 |
| Bounds absolutos | existe | aparece no overlay/manifesto | manter | P1 |
| Bounds relativos a janela | existe | `RelativeBoundingRect` no manifesto/UI | validar DPI/multimonitor | P0 |
| Estado enabled/visible/focused | existe | exposto como enabled/visible/focused quando aplicavel | validar variações UIA | P1 |
| Tree path / hierarquia de UI | falta | nao ha no produto | melhora robustez | P2 |
| Seletor salvo como ativo | existe | biblioteca do `Mira` lista, busca, detalha, aplica e apaga | editar nome/tags ainda e P1 | P0 |
| Testar seletor salvo | existe | bridge/UI possuem `Test Selector` | validar em apps reais | P0 |
| Score de robustez do seletor | existe | `SelectorStrengthEvaluator` expõe forte/media/fraca | calibrar heuristica | P0 |
| Estrategia sugerida (`selector`, `relative`, `absolute`, `image`) | existe | estrategia recomendada aparece no overlay/biblioteca | calibrar fallback | P0 |
| Wait element | existe | `action.desktopWaitElement` | manter | P0 |
| Click element | existe | `action.desktopClickElement` com fallback coordenado quando Invoke falha | validar apps elevados/hibridos | P0 |
| Type into element | parcial | `BrowserType` existe | tornar desktop first | P1 |
| Read element text | existe | `action.desktopReadElementText` | manter | P0 |
| Trigger element appeared | existe | `trigger.desktopElementAppeared` | validar apps reais | P0 |
| Trigger element disappeared | falta | nao existe | importante | P1 |
| Trigger element text changed | existe | `trigger.desktopElementTextChanged` | validar apps reais | P0 |
| Exists? com portas `found/notFound` | parcial | alguns nodes ja comecam a ir nessa linha | generalizar | P1 |
| Toggle checkbox / select list item / invoke menu | falta | nao ha nodes de alto nivel | cobertura de apps reais | P1 |

## F. Browser, Electron e Apps Hibridos

| Capacidade | Status | Realidade atual | Gap para produto | Prioridade |
|---|---|---|---|---|
| Selecionar elemento em app Electron via UIAutomation | parcial | depende do app cooperar | melhorar fallback e recipes | P0 |
| Filtrar por `processName` | parcial | em progresso/necessario | consolidar em todos os seletores | P0 |
| Filtrar por `processPath` | falta | ainda nao e trilha madura em produto | essencial para `Trae.exe` | P0 |
| `windowTitle` com `contains` | parcial | necessario/esperado | padronizar | P0 |
| `windowTitle` com regex | falta | nao e experiencia consolidada | cobertura avancada | P2 |
| Esperar navegador/app hibrido carregar estado | parcial | composicao manual | recipes melhores | P1 |
| Trigger por evento de navegador | falta | usuario ainda pensa em termos vagos | transformar em recipes concretos | P1 |
| Downloads / uploads / file pickers | falta | nao ha trilha nativa de produto | importante para browser real | P2 |
| URL / aba / titulo de browser | falta | nao ha suporte nativo geral | cobertura real web | P2 |

## G. Visao Computacional, OCR e Fallback Visual

| Capacidade | Status | Realidade atual | Gap para produto | Prioridade |
|---|---|---|---|---|
| Trigger por imagem | existe | `trigger.imageDetected` | fortalecer UX | P1 |
| Trigger por pixel | existe | `trigger.pixelChange` | fortalecer UX | P2 |
| Biblioteca de `Snip` | parcial | persistencia existe, UX ainda incompleta | finalizar catalogo | P0 |
| Match por imagem como action | existe | `action.clickImageMatch` clica no centro do match | validar DPI/escala | P0 |
| Wait por imagem | falta | hoje recai em trigger, nao action clara | criar action/recipe | P1 |
| OCR de regiao | falta | nao existe como produto nesta rodada | importante para apps nao acessiveis | P0 |
| OCR de elemento capturado | falta | nao existe como produto | muito util | P1 |
| Edicao manual de OCR persistido | falta | previsto, ainda nao existe | tornar OCR utilizavel | P1 |
| Cor/pixel inspector no `Mira` | existe | cor sob cursor aparece no overlay/manifesto | validar multimonitor/DPI | P1 |
| Template scaling / DPI awareness visual | falta | nao ha camada de produto | necessario em multiplos monitores | P1 |
| Pipeline oficial de fallback | parcial | selector/relative/image/absolute documentados e `clickImageMatch` existe | OCR e fallback automatico completo ainda faltam | P0 |
| Overlay de cor solida | existe | `action.overlayColor` cobre fullscreen/regiao com timer, plano, opacidade, motion e click-through | validar multimonitor/DPI | P1 |
| Overlay de imagem | existe | `action.overlayImage` suporta fit, background, timer, plano, fullscreen e motion | adicionar seletor de arquivo nativo melhor | P1 |
| Overlay de texto | existe | `action.overlayText` suporta fonte, cor, efeito, alinhamento, background, timer, plano, fullscreen e motion | presets de produto podem melhorar UX | P1 |

## H. Explorer, Dialogs e Shell Do Usuario

| Capacidade | Status | Realidade atual | Gap para produto | Prioridade |
|---|---|---|---|---|
| Automacao de dialogos Open/Save | falta | nao ha trilha nativa clara | muito comum no Windows | P1 |
| Automacao de Explorer | falta | sem recipes/nodes claros | muito comum | P1 |
| Context menu / right-click shell | falta | sem suporte guiado | muito util | P2 |
| Drag and drop de arquivos | parcial | `mouseDrag` existe, mas nao e semantico | action/recipe nativo | P2 |
| Trigger clipboard changed | falta | clipboard existe, trigger nao | automacoes leves | P2 |
| Toast/notification trigger | falta | sem trigger nativo | automacoes de suporte | P2 |

## I. Arquivos, Dados e Integracoes

| Capacidade | Status | Realidade atual | Gap para produto | Prioridade |
|---|---|---|---|---|
| Read/write file | existe | ja implementado | manter | P1 |
| CSV/JSON basico | existe | nodes ja existem | manter | P2 |
| HTTP request | existe | ja implementado | manter | P2 |
| Email | existe | ja implementado | manter | P2 |
| Banco de dados | falta | nao ha nodes nativos | ampliar uso corporativo | P2 |
| Webhooks/event callbacks | falta | nao ha trilha formal | integracao moderna | P2 |
| Segredos/credenciais protegidas | falta | nao ha camada clara de produto | necessario para producao | P1 |

## J. Assets, Biblioteca e Reuso

| Capacidade | Status | Realidade atual | Gap para produto | Prioridade |
|---|---|---|---|---|
| Biblioteca de `Snip` pesquisavel | falta | catalogo inicial ainda nao virou UX completa | finalizar | P0 |
| Biblioteca de `Mira` pesquisavel | falta | ainda nao virou UX completa | implementar | P0 |
| Tags, notas e rename | falta | catalogo ainda inicial | importante para escala | P1 |
| Preview detalhado do ativo | parcial | `Snip` parcial, `Mira` fraco | padronizar | P1 |
| Aplicar ativo ao node selecionado | parcial | `Use Latest`/bindings parciais existem | tornar universal | P0 |
| Detectar ativo quebrado | falta | nao ha health check de asset | muito util | P2 |
| Export/import de assets | falta | nao ha catalogo local completo | necessario para compartilhamento | P2 |

## K. UX, Recipes e Criacao Guiada

| Capacidade | Status | Realidade atual | Gap para produto | Prioridade |
|---|---|---|---|---|
| Editor visual base | existe | canvas, palette, property panel ok | manter | P0 |
| Recipe/wizard de desktop automation | falta | nao existe | essencial para usuario final | P0 |
| Recipe `wait text then click` | falta | nao existe | essencial | P0 |
| Recipe `Trae auto-continue` | falta | enquanto nao validado real, nao existe como produto | tornar sample oficial util | P0 |
| Recipe popup auto-confirm | falta | nao existe | muito util | P1 |
| Recipe horario + repeticoes | falta | nao existe | muito util | P1 |
| Macro recorder visual | falta | nao existe | enorme ganho de UX | P1 |
| Validacao pre-armar | parcial | ha validacoes tecnicas, nao jornada clara | tornar explicito | P0 |
| Hints contextuais no editor | falta | ainda fraco | melhorar onboarding | P1 |
| Exemplos reais uteis | parcial | muitos examples ainda mock-like | substituir/complementar | P0 |

## L. Observabilidade, Diagnostico e Debug

| Capacidade | Status | Realidade atual | Gap para produto | Prioridade |
|---|---|---|---|---|
| Logs basicos | existe | ja ha logs e eventos | enriquecer | P0 |
| Runtime status global | existe | UI mostra estado basico | ampliar semantica | P0 |
| Timeline de execucao | falta | usuario ainda nao ve bem a historia | implementar | P0 |
| Estados semanticos (`waiting`, `matched`, `retrying`, `cooldown`) | falta | ainda opaco para usuario | implementar | P0 |
| Erro explicito por node/selector | parcial | alguns erros existem, mas UX ainda pobre | melhorar | P1 |
| Screenshot on failure | falta | nao existe | muito util para suporte | P2 |
| Dry run / debug step by step | falta | nao existe | muito util | P2 |
| Export de pacote de suporte | falta | nao existe | necessario para suporte | P1 |
| Diagnostico de seletor | falta | nao existe como fluxo guiado | muito util | P1 |

## M. Seguranca, Governanca e Operacao

| Capacidade | Status | Realidade atual | Gap para produto | Prioridade |
|---|---|---|---|---|
| Limites contra loops destrutivos | falta | guardas globais ainda insuficientes | cooldown/debounce/max repeat | P0 |
| Kill switch local | falta | nao ha botao/atalho global forte de emergencia | necessario | P1 |
| Secrets handling | falta | sem camada clara | necessario | P1 |
| Permissoes/capabilities por pacote | parcial | Marketplace local de recipes existe; marketplace remoto segue bloqueado sem assinatura/capabilities | preparar governanca remota | P2 |
| Redacao de logs sensiveis | falta | nao ha politica clara | necessario | P2 |

## N. Packaging, Publish e Compatibilidade

| Capacidade | Status | Realidade atual | Gap para produto | Prioridade |
|---|---|---|---|---|
| Publish desktop consistente | existe | publish oficial revalidado em `src/Ajudante.App/bin/publish` | manter gate de exe correto | P0 |
| Seed flows uteis no publish | parcial | ainda precisam ser menos mock e mais reais | revisar | P0 |
| Validacao do exe publicado | parcial | precisa virar gate obrigatorio | institucionalizar | P0 |
| Upgrade/migracao segura | parcial | base de `AppPaths` existe | ampliar para novos assets | P1 |
| Instalação/rollback documentados | falta | ainda nao maduros | importante para suporte | P2 |
| Matriz de compatibilidade Windows | falta | ainda nao formal | obrigatorio para producao | P0 |

## O. Testes, QA e Validacao Real

| Capacidade | Status | Realidade atual | Gap para produto | Prioridade |
|---|---|---|---|---|
| Unit tests core/runtime | existe | base de testes ja boa | manter | P0 |
| Tests de frontend | existe | base existe | ampliar casos de UX real | P1 |
| Testes de selectors desktop | parcial | ainda insuficientes | ampliar | P0 |
| E2E desktop real | falta | ainda nao formalizado como suite | necessario | P0 |
| Soak tests 8h/24h | falta | ainda nao formalizados | necessario | P1 |
| Validacao manual no app alvo real | parcial | ocorre pontualmente, nao como gate | institucionalizar | P0 |
| Teste oficial do caso Trae | falta | sem prova real final, nao fechado | necessario | P0 |

## Mapa De Prioridade Operacional

### P0 - Necessario para o produto deixar de ser beta tecnico

1. `Mira` rico e realmente utilizavel
2. biblioteca de `Mira` funcional na UI
3. filtro por `processPath`
4. trigger de `desktop element appeared`
5. `desktop click/read/wait` com semantica de produto
6. fallback visual oficial (`clickImageMatch`, OCR inicial, pipeline de fallback)
7. scheduler minimo (`horario fixo` e `intervalo`)
8. acoes de janela (`focus`, `bring to front`, `minimize`, `restore`)
9. states semanticos de runtime na UI
10. sample/recipe oficial do `Trae auto-continue` validado no exe publicado

### P1 - Muito importante para cobertura ampla e confiavel

1. recorder de macro
2. score de seletor
3. `Test Selector`
4. recipes guiados
5. retry/timeout/cooldown configuraveis
6. export de suporte
7. automacao de dialogs/Explorer
8. OCR editavel e persistido
9. package local de flow + assets

### P2 - Escala, polimento e governanca

1. subflows
2. cron-like scheduling
3. package governance
4. marketplace remoto
5. screenshot on failure
6. debug step by step
7. multienvironment automation matrix mais ampla

## Definicao De Produto Windows Realmente Completo

Para considerar o Sidekick `completo para Windows nas mais diversas situacoes`, os itens abaixo devem estar verdadeiros ao mesmo tempo:

1. usuario consegue automatizar por horario, por janela, por processo, por elemento, por imagem e por evento simples
2. usuario consegue interagir com mouse, teclado, janelas, Explorer, dialogs e apps hibridos
3. `Mira` e `Snip` geram ativos reutilizaveis com UX madura
4. existe fallback honesto quando UIAutomation falha
5. runtime e observavel e previsivel
6. exemplos e recipes sao reais e validados
7. o executavel publicado foi testado em cenarios Windows reais

## Ordem Obrigatoria De Implementacao

1. `Mira` de produto + biblioteca de inspecoes
2. desktop selectors e actions/triggers nativos
3. fallback visual/OCR
4. acoes de janela/processo
5. scheduler e automacoes temporizadas
6. recipes/wizards e exemplos reais
7. observabilidade e suporte
8. package local e governanca
9. matriz formal de compatibilidade

## Regra Final Para Outro Agente

Outro agente nao deve tratar nenhum item desta matriz como `existe` apenas porque ha base tecnica no backend.

Uma capacidade so deve ser promovida de `parcial` para `existe` quando:

- houver UX utilizavel no produto
- houver contrato estavel
- houver teste automatizado relevante
- houver validacao manual no executavel publicado quando aplicavel
- `PROJECT_STATUS.md` for atualizado
