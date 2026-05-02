# RFC_PRODUCT_STRENGTHENING.md

## Status

- Estado: `aprovado para implementacao incremental`
- Data de consolidacao: `2026-04-25`
- Documento de acompanhamento operacional: `PROJECT_STATUS.md`

Atualizacao `2026-04-29`:

- Fases de fortalecimento P0 avancaram para release candidate tecnico.
- `Mira` foi promovido para inspecao rica com biblioteca, score de seletor, estrategia recomendada, teste de seletor, preview e reaplicacao no node selecionado.
- Desktop automation ganhou API publica de produto com selectors por janela/processo/caminho e actions/triggers nativos.
- Recipes oficiais foram adicionados em `flows/` para desktop automation, wait text then click, popup auto-confirm, Trae auto-continue, fallback visual e scheduler.
- Marketplace remoto, package governance e OCR editavel continuam fora do RC desta rodada.

Atualizacao `2026-04-30`:

- Criacao de fluxo recebeu ajuste de produto: soltar fio sem conexao abre o menu `Adicionar passo`, e a conexao usa raio maior para facilitar ligacao magnetica.
- Catalogo local aparece como Marketplace na toolbar para recipes oficiais.
- A cobertura Windows foi ampliada com nodes de hardware/sistema para audio, microfone, camera, Wi-Fi, energia e display.
- Operacoes que mudam sistema exigem guardas explicitas (`allowSystemChanges` ou frase `CONFIRM`), mantendo a importacao/execucao remota como fora de escopo seguro.
- Foi adicionada trilha de captura/gravao para produto:
  - `action.captureScreenshot`
  - `action.recordDesktop`
  - `action.recordCamera`
- `Mira` recebeu metadados adicionais de resiliencia (estado de janela, monitor, pontos relativos/normalizados, handle quando seguro) e prefill para pipeline de fallback em nodes desktop.
- `logic.conditionGroup` passou a suportar `ANY/ALL`, operadores textuais/numericos, nested groups simples e operador `changed`.
- Recipes Marketplace locais adicionais foram entregues para screenshot, gravacao desktop/camera, clique resiliente Mira e assistente WhatsApp em modo draft seguro.
- Limite declarado e mantido: gravacao de audio ainda fora de escopo desta entrega.

Atualizacao `2026-04-30` (editor visual P0):

- A Fase 3 de jornada guiada avancou no canvas real:
  - menu de contexto no canvas, node e edge
  - criacao de proximo passo ao soltar fio no vazio
  - insercao de node no meio de uma edge existente
  - reconexao/remocao de edge com validacao local de porta
  - duplicar node e habilitar/desabilitar sem apagar
  - bypass de node desabilitado na view de runtime quando ha uma entrada e uma saida
  - auto layout basico e atalhos (`Ctrl+D`, `Ctrl+0`, `Ctrl+K`/`/`, `C`, `L`)
- O palette adotou categorias publicas de produto: Trigger, Desktop, Window, Hardware, Media, Console, Logic, Data e Utility.
- A conversao frontend/backend preserva metadata de UI somente quando solicitado e separa `runtimeView` para execucao/validacao.
- Validado nesta rodada: `dotnet build`, `dotnet test`, `npm run test`, `npm run build` e `dotnet publish` oficial.

Atualizacao `2026-05-02`:

- Sticky notes deixaram de ser apenas annotation visual fragil e agora possuem caminho completo de produto: criar, editar, mover, redimensionar, duplicar, remover, salvar e recarregar.
- Mira/Mira Lib avançaram para biblioteca reutilizavel com schema versionado, thumbnail, nome/notas/tags, busca por texto/processo/janela/tag, teste de seletor, duplicacao e exclusao.
- Contrato de texto do Mira foi ampliado para `Name`, `ValuePattern`, `TextPattern`, Legacy, `HelpText`, placeholder/hint e origem/qualidade. OCR local ainda nao esta empacotado, entao o fallback e mostrado honestamente como indisponivel quando aplicavel.
- Cobertura Windows foi expandida com nodes seguros de taskbar, tema, wallpaper, desktop, Explorer, restore point, admin, instalacao resiliente, persistencia de estado e guardas de tempo.
- Foi criada a recipe Marketplace `Tempo de Jogo - ROBLOX`, com processo real, timer de 2 minutos, overlay, sons, fechamento gracioso, kill fallback, cooldown e bloqueio persistido ate meia-noite.
- Export Runner inicial implementado como pacote semi-autonomo com `flow.json`, arquivos do app e `run-sidekick-flow.cmd`; single EXE embutido segue fora desta fase.

## Objetivo

Fortalecer o produto alem da base tecnica atual, priorizando:

1. jornada mais facil para criacao de fluxos
2. evolucao do `Snip` para biblioteca de ativos reutilizaveis
3. evolucao do `Mira` para inspecao orientada a automacao real
4. fundacao segura para catalogo local e marketplace futuro

Este RFC nao assume marketplace remoto imediato. A estrategia aprovada e construir primeiro os contratos locais, a validacao e a confiabilidade.

## Motivacao

O produto ja possui editor, runtime, bridge e publish funcional. O proximo gargalo nao e mais "conseguir executar", e sim:

- reduzir esforco para criar automacoes
- transformar capturas e inspecoes em ativos reutilizaveis
- aumentar previsibilidade das macros em ambientes diferentes
- permitir evolucao do ecossistema sem quebrar compatibilidade

## Principios Obrigatorios

Toda implementacao desta fase deve obedecer aos principios abaixo:

- honestidade funcional: nao prometer deteccao, adaptacao ou inteligencia que nao exista de fato
- escopo fechado por fase: nada de abrir frentes grandes sem contrato minimo
- zero ambiguidade de estado: cada entrega deve atualizar `PROJECT_STATUS.md`
- compatibilidade primeiro: formatos persistidos e contratos bridge devem ser versionados
- rollback simples: cada entrega deve ser pequena, testavel e reversivel
- telemetria local explicita: logs e mensagens claras quando um recurso falhar

## Fora De Escopo Imediato

Nao faz parte da fase inicial:

- marketplace remoto publico
- execucao distribuida
- sincronizacao em nuvem
- IA autonoma tomando decisoes sem configuracao explicita do usuario
- importacao irrestrita de plugins sem manifesto, validacao e politica de permissao

## Frentes De Trabalho

### Frente A. Snip Asset Library

Objetivo:

Transformar capturas feitas pelo `Snip` em ativos persistidos e reutilizaveis na construcao de flows.

Escopo funcional minimo:

- salvar cada captura como ativo catalogado
- exibir biblioteca visual semelhante ao conceito da aba de nodes
- registrar metadados minimos por ativo:
  - `id`
  - `createdAt`
  - `sourceProcessName`
  - `windowTitle`
  - `windowClassName`
  - `captureBounds`
  - `imagePath`
  - `ocrText`
  - `ocrConfidence`
  - `notes`
  - `tags`
- permitir ajuste manual do texto OCR
- permitir uso do ativo como referencia em nodes compatíveis

Nao prometer nesta fase:

- OCR perfeito
- matching auto-adaptativo sem validacao
- banco remoto

### Frente B. Mira Inspection Library

Objetivo:

Transformar o `Mira` em uma ferramenta de inspecao util para automacao real, com dados curados e reaproveitaveis.

Camadas de dados aprovadas:

1. essencial
2. posicionamento
3. contexto de execucao
4. avancado

Detalhamento minimo:

- essencial:
  - nome visivel
  - automation id
  - class name
  - control type
  - process name
  - process id
  - window title
- posicionamento:
  - bounds absolutos
  - posicao relativa a janela
  - resolucao do host
  - estrategia de recalculo relativa
- contexto de execucao:
  - processo pai quando disponivel
  - filhos relevantes quando disponivel
  - foco
  - estado de habilitado/visivel
- avancado:
  - consumo de memoria do processo
  - atividade de disco quando disponivel por API segura
  - outros dados apenas se forem confiaveis e com custo aceitavel

Entrega de produto:

- salvar inspecoes como ativos reutilizaveis
- recomendar estrategia de localizacao:
  - `selectorPreferred`
  - `relativePositionFallback`
  - `absolutePositionLastResort`

### Frente C. Jornada Guiada De Flows

Objetivo:

Diminuir a friccao entre capturar dados e montar uma macro utilizavel.

Escopo funcional minimo:

- insercao de ativo de `Snip` diretamente no fluxo
- insercao de ativo de `Mira` diretamente no fluxo
- assistencia de configuracao para nodes mais sensiveis
- presets honestos para cenarios comuns
- mensagens de validacao mais claras antes da execucao

Nao inclui nesta fase:

- geracao automatica completa de fluxos por IA
- refactor grande do editor visual

### Frente D. Catalogo Local E Marketplace Futuro

Objetivo:

Criar uma base segura para distribuir flows, plugins e add-ons primeiro localmente e depois remotamente.

Fase inicial aprovada:

- formato de pacote local
- manifesto versionado
- validacao de compatibilidade
- import/export manual
- rollback simples em caso de falha de instalacao

Tipos de pacote previstos:

- `flow-pack`
- `plugin-pack`
- `asset-pack`
- `integration-pack`

Marketplace remoto:

- fica bloqueado ate o catalogo local estar confiavel

## Ordem De Implementacao Obrigatoria

1. `Snip Asset Library`
2. `Mira Inspection Library`
3. `Jornada Guiada De Flows`
4. `Catalogo Local`
5. `Marketplace Remoto`

Motivo:

- `Snip` e `Mira` geram os ativos
- a jornada guiada depende desses ativos
- o empacotamento depende de formatos estabilizados
- o marketplace remoto depende da governanca do catalogo local

## Contratos Iniciais

### 1. Asset Catalog

Arquivo conceitual:

- raiz proposta: `%AppData%/Sidekick/assets/`

Subpastas propostas:

- `snips/`
- `inspections/`
- `manifests/`
- `thumbnails/`

Schema inicial de manifesto de ativo:

```json
{
  "id": "uuid",
  "kind": "snip|inspection",
  "version": 1,
  "createdAt": "2026-04-25T12:00:00Z",
  "updatedAt": "2026-04-25T12:00:00Z",
  "displayName": "Botao Confirmar",
  "tags": ["erp", "confirmar"],
  "notes": "Usado no fluxo de faturamento",
  "source": {
    "processName": "erp.exe",
    "windowTitle": "ERP - Pedido",
    "windowClassName": "WindowsForms10.Window.8.app.0.141b42a_r7_ad1"
  },
  "locator": {
    "strategy": "selectorPreferred",
    "selector": {},
    "relativeBounds": {},
    "absoluteBounds": {}
  },
  "content": {
    "imagePath": "assets/snips/uuid.png",
    "ocrText": "Confirmar",
    "ocrConfidence": 0.91
  }
}
```

### 2. Package Manifest

Schema inicial:

```json
{
  "id": "sidekick.pack.example",
  "type": "flow-pack",
  "version": "1.0.0",
  "schemaVersion": 1,
  "displayName": "Fluxos de Financeiro",
  "author": "Equipe",
  "description": "Pacote local de fluxos validados",
  "requires": {
    "appVersionMin": "1.0.0",
    "nodeTypeIds": ["action.httpRequest", "logic.ifElse"]
  },
  "contents": [
    {
      "kind": "flow",
      "path": "flows/financeiro_aprovacao.json"
    }
  ]
}
```

### 3. Bridge / UI

Os contratos abaixo sao candidatos aprovados para evolucao incremental. Cada item so deve ser implementado com documentacao e teste minimo.

Novos comandos provaveis:

- `assets/listAssets`
- `assets/getAsset`
- `assets/saveSnipAsset`
- `assets/saveInspectionAsset`
- `assets/deleteAsset`
- `packages/listPackages`
- `packages/importPackage`
- `packages/exportPackage`

Implementado no recorte atual de `Snip`:

- `assets/listSnipAssets`
- `assets/getSnipAssetTemplate`

Novos eventos provaveis:

- `assets/assetSaved`
- `assets/assetDeleted`
- `packages/packageImported`
- `packages/packageImportFailed`

Implementado no recorte atual de `Snip`:

- `assets/snipAssetSaved`

Regra:

- nenhuma action nova deve entrar em producao sem atualizar `src/Ajudante.UI/src/bridge/types.ts` e o documento operacional correspondente

## Riscos E Mitigacoes

### Risco 1. Persistencia sem versao

Impacto:

- quebra silenciosa de compatibilidade

Mitigacao:

- todo manifesto deve ter `version` e `schemaVersion`
- migracoes devem ser explicitas e testadas

### Risco 2. Coleta excessiva de dados no Mira

Impacto:

- lentidao, ruido e falsas expectativas

Mitigacao:

- separar dados em camadas
- marcar campos experimentais
- nao expor metricas instaveis como se fossem garantidas

### Risco 3. Ativos ruins contaminando flows

Impacto:

- macros fragilizadas e de dificil manutencao

Mitigacao:

- exigir metadados minimos
- mostrar estrategia de localizacao adotada
- permitir revisao manual antes de anexar ao flow

### Risco 4. Marketplace prematuro

Impacto:

- incompatibilidade, seguranca fraca e rollback ruim

Mitigacao:

- bloquear remoto ate haver catalogo local validado
- exigir manifesto, validacao de compatibilidade e instalacao reversivel

### Risco 5. Ambiguidade documental

Impacto:

- agentes repetem trabalho, reabrem decisoes e quebram contratos

Mitigacao:

- atualizar `PROJECT_STATUS.md` a cada entrega relevante
- registrar fase, contratos e limites neste RFC
- manter `AGENTS.md` focado em regras permanentes

## Criterios De Aceite Por Fase

### Fase 1. Snip Asset Library

- captura pode ser salva como ativo persistido
- biblioteca lista ativos com preview e metadados essenciais
- OCR pode ser ajustado manualmente
- pelo menos um node consegue consumir o ativo salvo
- testes cobrem serializacao e leitura dos manifestos

### Fase 2. Mira Inspection Library

- inspecao pode ser salva como ativo persistido
- UI mostra camadas de dados curadas sem excesso inutil
- estrategia recomendada de localizacao fica explicita
- pelo menos um fluxo consegue reutilizar o ativo
- testes cobrem serializacao do ativo e regras de fallback
- Status `2026-04-29`: concluido para RC tecnico com lista, busca, preview, detalhes, score, `Test Selector`, apagar e aplicar ao node selecionado.

### Fase 3. Jornada Guiada

- usuario consegue inserir um ativo salvo no fluxo com menos friccao
- mensagens de erro e validacao ficam mais claras
- nao ha regressao nas operacoes atuais de salvar, carregar e executar
- Status `2026-04-29`: parcial para RC; recipes oficiais e samples validaveis existem, mas wizard dedicado ainda nao foi implementado.
- Status `2026-04-30`: RC tecnico do editor P0 avancado; usuario ja consegue criar fluxo pelo canvas/menu, inserir node no meio de edge, reconectar/remover edge, duplicar/desabilitar node e organizar com auto layout. Ainda falta validacao manual ampla no executavel publicado.

### Fase 4. Catalogo Local

- pacote local pode ser exportado e importado com manifesto
- validacao rejeita pacote incompativel
- falha de instalacao nao deixa estado parcial silencioso
- Status `2026-04-30`: catalogo local de recipes oficiais existe na toolbar e e publicado como `seed-flows/*.json`; remoto/publicacao de pacote continua bloqueado ate hash/assinatura, schema, capabilities e importacao desarmada ficarem completos.

## Protocolo De Registro Obrigatorio

Sempre que uma entrega desta frente avancar, o agente deve:

1. atualizar `PROJECT_STATUS.md` com o estagio real
2. atualizar este RFC se escopo, contratos ou riscos mudarem
3. mencionar o que foi realmente validado
4. nao marcar fase como concluida sem evidencias objetivas

## Primeira Implementacao Recomendada

O primeiro bloco tecnico recomendado e:

1. criar modelo persistido para `SnipAssetManifest`
2. definir pasta de ativos e servico local de catalogo
3. expor listagem inicial para a UI
4. permitir salvar uma captura ja existente como ativo
5. adicionar testes focados de serializacao e catalogo

Esse caminho gera valor real rapido, tem baixo risco de regressao e prepara as frentes seguintes.
