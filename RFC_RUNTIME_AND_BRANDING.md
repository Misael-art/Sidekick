# RFC: Integracao De Triggers Continuos E Consolidacao Ajudante / Sidekick

## Objetivo

Este documento descreve um plano de evolucao para duas frentes estruturais do projeto:

1. integrar de forma efetiva os triggers continuos ao runtime principal do app
2. consolidar a coexistencia de naming entre `Ajudante` e `Sidekick`

O foco e transformar essas duas areas em capacidades previsiveis, sustentaveis e seguras para evolucao futura.

## Contexto

Hoje o projeto possui:

- uma base funcional de execucao manual de flows via `engine/runFlow`
- infraestrutura parcial para triggers continuos no core
- coexistencia de naming entre o repositorio e namespaces `Ajudante.*` e o branding/runtime `Sidekick`

As duas frentes afetam diretamente:

- arquitetura
- UX
- persistencia
- bridge UI/backend
- publish e distribuicao
- compatibilidade com dados do usuario

## Escopo

Este RFC cobre:

- modelo operacional para flows monitorados por triggers
- integracao de `TriggerManager` ao host desktop
- ampliacao do contrato da bridge para runtime continuo
- evolucao da UI para ativacao e monitoramento de flows
- estrategia de consolidacao de nome do produto
- estrategia de compatibilidade e migracao de dados

Este RFC nao cobre em detalhe:

- redesign visual completo da UI
- reescrita do executor para paralelismo avancado
- rename completo de todos os namespaces em uma unica etapa

## Objetivos De Resultado

Ao final da iniciativa, queremos:

- flows com triggers que possam ser ativados e permanecer armados
- disparo automatico de execucao quando o trigger ocorrer
- estado de runtime claro para UI e backend
- limpeza correta de watchers ao desarmar ou encerrar o app
- um nome canônico claro para o produto
- compatibilidade com dados existentes do usuario durante a transicao

## Situacao Atual

### Runtime

O projeto possui infraestrutura de trigger em:

- `src/Ajudante.Core/Engine/TriggerManager.cs`
- `src/Ajudante.Core/Interfaces/ITriggerNode.cs`

Mas o host principal esta centrado na execucao pontual via:

- `engine/runFlow`
- `engine/stopFlow`
- `engine/getStatus`

Ou seja:

- o desenho conceitual de trigger continuo existe
- a integracao operacional com o app principal nao esta fechada
- a UX atual parece orientada a `Play/Stop` manual

### Naming

O projeto possui coexistencia entre:

- `Ajudante` em repositorio, nomes de projeto e namespaces
- `Sidekick` em `AssemblyName`, `%AppData%`, mutex e mensagens de runtime

Isso gera ambiguidades em:

- documentacao
- suporte
- logs
- scripts
- empacotamento
- manutencao futura

## Principios Norteadores

- evoluir por etapas pequenas e verificaveis
- separar definicao de flow de estado efemero de runtime
- manter compatibilidade com dados existentes
- nao fazer rename tecnico massivo antes de fechar o branding canônico
- distinguir claramente `flow armado` de `flow em execucao`
- projetar contratos explicitos de bridge antes de expandir UI

## Frente 1: Integracao De Triggers Continuos

## 1. Problema

O core ja possui base para watchers e triggers assíncronos, mas o aplicativo nao opera ainda como um runtime continuo de automacao.

Na pratica, isso impede:

- flows monitorados de longa duracao
- experiencia consistente de automacao reativa
- representacao clara de estado entre trigger, runtime e UI

## 2. Objetivo Funcional

O sistema deve permitir que um flow:

- seja executado manualmente uma vez
- seja armado para monitoramento continuo
- dispare execucao automaticamente quando seu trigger ocorrer
- possa ser desarmado sem reiniciar o app

## 3. Modelo Operacional Proposto

Definir tres modos conceituais:

### 3.1 Flow Manual

- executa uma vez quando o usuario usa `Play`
- nao permanece observando eventos

### 3.2 Flow Monitorado

- fica armado
- mantem triggers ativos em background
- dispara quando um evento relevante ocorrer

### 3.3 Flow Hibrido

- pode ser executado manualmente
- tambem pode ficar armado para monitoramento continuo

## 4. Modelo De Estado Proposto

A UI e o backend devem distinguir ao menos:

- `inactive`
- `armed`
- `triggered`
- `running`
- `error`

Estados importantes adicionais:

- `lastTriggerAt`
- `lastRunAt`
- `lastError`
- `activeTriggerNodeIds`

Nao usar apenas `isRunning` como representacao global.

Separacao recomendada:

- `isArmed`: ha trigger ativo observando eventos
- `isRunning`: ha execucao efetiva acontecendo agora

## 5. Politica De Concorrencia

E necessario definir o que acontece se um trigger disparar enquanto o flow ainda esta em execucao.

Alternativas:

- ignorar novo disparo
- enfileirar disparos
- cancelar execucao atual e reiniciar

Recomendacao para primeira versao:

- ignorar novo disparo se o flow ja estiver rodando
- registrar log explicito indicando descarte do novo trigger

Justificativa:

- menor complexidade
- menor risco de reentrancia inesperada
- previsibilidade operacional

## 6. Arquitetura Proposta

Criar um orquestrador de runtime no app, por exemplo:

- `FlowRuntimeManager`
- ou `AutomationRuntimeService`

Esse servico deve centralizar:

- `FlowExecutor`
- `TriggerManager`
- estado dos flows ativos
- coordenacao com a bridge
- logs e eventos de runtime

O `MainWindow` deixa de ser o coordenador direto do runtime e passa a usar esse servico.

## 7. Responsabilidades Do Runtime Unificado

O novo runtime deve:

- executar flow manual
- ativar triggers de um flow
- desativar triggers de um flow
- listar flows ativos
- expor estado resumido do runtime para a UI
- propagar eventos de trigger e execucao
- garantir cleanup completo no encerramento

## 8. Evolucao Do Contrato Da Bridge

Manter comandos existentes:

- `engine/runFlow`
- `engine/stopFlow`
- `engine/getStatus`

Adicionar comandos novos:

- `engine/activateFlow`
- `engine/deactivateFlow`
- `engine/listActiveFlows`
- `engine/getRuntimeStatus`

Adicionar eventos novos:

- `engine/flowArmed`
- `engine/flowDisarmed`
- `engine/triggerFired`
- `engine/flowSkippedBecauseAlreadyRunning`
- `engine/runtimeError`

## 9. Evolucao Da UI

A UI deve evoluir para diferenciar:

- executar agora
- ativar monitoramento
- parar monitoramento

Mudancas recomendadas:

- botao ou toggle de `Armar`
- indicador visual de flow armado
- estado do runtime na status bar
- listagem de flows monitorados
- logs especificos para trigger fired e skipped triggers

## 10. Persistencia De Estado

Separar:

- definicao estrutural do flow
- estado efemero de runtime

Recomendacao:

- nao gravar `armed` diretamente no JSON principal do flow como estado transitório
- usar metadados separados para preferencia de runtime

Exemplo de separacao:

- `flow.json`: definicao do flow
- `runtime-state.json`: preferencias e ultimo estado operacional

Persistir apenas metadados que facam sentido entre reinicios, por exemplo:

- `autoArmOnStartup`
- `lastKnownRuntimeError`
- `lastSuccessfulRunAt`

## 11. Robustez E Lifecycle

Cada trigger deve ter contrato confiavel de:

- `StartWatchingAsync`
- `StopWatchingAsync`
- cancelamento
- descarte

O runtime deve garantir:

- parada limpa ao desarmar
- parada limpa ao fechar o app
- logs consistentes em erro de trigger
- isolamento entre erro de trigger e queda do app inteiro

## 12. Testes Recomendados Para Triggers

Automatizados:

- ativacao de trigger
- desativacao de trigger
- trigger dispara execucao
- trigger dispara enquanto flow esta rodando
- cleanup ao encerrar
- restauracao de `autoArmOnStartup`, se implementado

Manuais:

- hotkey trigger
- file system trigger
- image detected trigger
- pixel change trigger
- window event trigger

## 13. Criterio De Aceite Para Triggers

Consideraremos essa frente efetiva quando:

- um flow puder ser armado pela UI
- o app mantiver o watcher ativo sem travar a UI
- o trigger disparar execucao automaticamente
- o backend publicar estado correto para a UI
- o monitoramento puder ser parado sem reiniciar
- o encerramento do app limpar watchers corretamente

## Frente 2: Consolidacao Ajudante / Sidekick

## 14. Problema

Hoje existe duplicidade de identidade no projeto:

- `Ajudante` como identidade de repositório e nomes tecnicos
- `Sidekick` como identidade de runtime em varios pontos

Isso dificulta:

- consistencia de documentacao
- entendimento por novos contribuidores
- suporte e troubleshooting
- estabilidade de naming em scripts e instalacao

## 15. Objetivo

Definir um nome canônico do produto e aplicá-lo de forma progressiva, sem quebrar compatibilidade com dados existentes do usuario.

## 16. Decisao Necessaria

Antes de qualquer refactor, decidir:

- qual e o nome canônico externo do produto
- qual e o nome técnico oficial do executavel
- qual e o caminho oficial em `%AppData%`
- qual e a estrategia para nomes legados

Recomendacao pragmatica:

- escolher um nome canônico unico para branding externo
- manter compatibilidade de leitura com o legado por transicao
- postergar rename amplo de namespaces para depois

## 17. Inventario Tecnico

Mapear todas as ocorrencias de:

- `Ajudante`
- `Sidekick`

Categorias para o inventario:

- projetos e namespaces
- `AssemblyName`
- mensagens para usuario
- logs
- `%AppData%`
- mutex global
- instalador
- scripts
- docs
- publish output

## 18. Estrategia De Migracao

Nao executar rename global em uma unica etapa.

Executar por camadas:

### 18.1 Branding Externo

Unificar:

- titulos de janela
- mensagens
- docs
- instalador
- textos visiveis ao usuario

### 18.2 Storage E Compatibilidade

Definir pasta oficial de dados e suportar migracao automatica.

Exemplo de comportamento:

- preferir novo caminho oficial
- se nao existir, procurar caminho legado
- migrar dados automaticamente
- manter leitura defensiva por uma ou duas versoes

### 18.3 Runtime E Packaging

Consolidar:

- nome do executavel
- mutex global
- logs
- pasta de dados
- publish profile

### 18.4 Renome Tecnico Opcional

Somente depois de estabilizar branding e compatibilidade:

- renomear namespaces
- renomear projetos
- renomear pastas

Esse passo e opcional e deve ser avaliado por custo/beneficio.

## 19. Compatibilidade De Dados

Se a pasta oficial de `%AppData%` mudar, o app deve:

- detectar a pasta antiga
- localizar `flows`, `logs` e `plugins`
- copiar ou mover os dados de forma segura
- evitar perda de informacao

Comportamento recomendado:

- se a pasta nova nao existir e a antiga existir, migrar
- se ambas existirem, priorizar a nova e registrar log
- nunca apagar automaticamente a pasta antiga na primeira versao da migracao

## 20. Configuracao Centralizada De Branding

Criar uma fonte unica de verdade para identidade do produto, por exemplo:

- `ProductIdentity`
- `AppBranding`

Essa configuracao deve centralizar:

- nome do produto
- nome do executavel
- nome da pasta de dados
- nome do mutex
- textos padrão

Objetivo:

- eliminar strings soltas
- reduzir divergencias futuras

## 21. Testes Recomendados Para Naming E Migracao

Automatizados:

- resolucao do caminho oficial de dados
- deteccao de caminho legado
- comportamento de migracao
- leitura de flows apos upgrade

Manuais:

- instalacao limpa
- upgrade sobre instalacao antiga
- preservacao de flows
- preservacao de plugins
- gravacao de logs
- funcionamento de instancia unica

## 22. Criterio De Aceite Para Naming

Consideraremos a frente concluida quando:

- o usuario enxergar um unico nome do produto em toda a experiencia
- dados antigos continuarem acessiveis apos upgrade
- o app publicar e instalar com identidade consistente
- nao houver ambiguidade relevante entre docs, logs, runtime e packaging

## Roadmap Recomendado

## Sprint 1

- fechar decisao funcional para triggers continuos
- fechar decisao de nome canônico
- inventariar naming e pontos de runtime

## Sprint 2

- introduzir runtime unificado
- integrar `TriggerManager` ao host
- ampliar contrato da bridge

## Sprint 3

- ajustar UI para flows armados
- implementar testes automatizados de runtime continuo
- validar politica de concorrencia e cleanup

## Sprint 4

- unificar branding externo
- implementar migracao de pasta de dados
- validar publish e instalador

## Sprint 5

- decidir se vale renome tecnico de namespaces/projetos
- executar rename tecnico somente se o custo compensar

## Riscos Principais

- confundir `isRunning` com `isArmed`
- deixar watchers vivos apos fechamento ou desarme
- permitir concorrencia sem politica definida
- quebrar compatibilidade de `%AppData%`
- fazer rename massivo cedo demais e gerar ruido excessivo

## Recomendacao Final

Executar primeiro a integracao de runtime continuo com contrato claro de estado.

Em paralelo:

- consolidar branding externo
- definir estrategia de storage e compatibilidade

Postergar rename tecnico amplo para uma etapa posterior e opcional.

O fator critico de sucesso nao e apenas refactor textual, mas a combinacao de:

- modelo operacional claro
- bridge bem definida
- ciclo de vida confiavel
- compatibilidade segura com dados do usuario

## Entregaveis Recomendados

Para operacionalizar este RFC, os entregaveis iniciais recomendados sao:

- inventario de naming `Ajudante` / `Sidekick`
- desenho do runtime unificado
- proposta do novo contrato de bridge
- estrategia de migracao de `%AppData%`
- matriz de testes de triggers continuos
