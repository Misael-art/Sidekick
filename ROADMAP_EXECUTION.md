# Roadmap de Produto e Implementacao

## Objetivo

Transformar o editor de automacoes em um produto confiavel, facil de adotar e dificil de abandonar.

Cada entrega deve melhorar pelo menos um destes pontos:

- tempo ate primeiro valor
- taxa de sucesso dos flows
- facilidade de manutencao
- reutilizacao e compartilhamento

## Principios

- Primeiro reduzir friccao.
- Depois aumentar poder.
- Por fim escalar distribuicao e colaboracao.
- Priorizar user success, estabilidade operacional e reuso de flows.
- Validar cada entrega com testes verdes antes de considera-la efetiva.

## Horizontes

### Horizonte 1: Fundacao de Produto

- Validacao pre-run mais completa.
- Teste rapido de seletor e node.
- Historico de execucoes.
- Melhorias de primeira experiencia e confianca operacional.

### Horizonte 2: Aceleracao de Criacao

- Criacao guiada de nodes a partir do Mira.
- Reuso pratico de assets do Mira e Snip.
- Fluxo de criacao mais rapido com menos configuracao manual.

### Horizonte 3: Robustez de Automacao

- Observabilidade local e diagnostico melhor.
- Fluxos mais resistentes a mudancas de ambiente.
- Bases para debugging visual e manutencao simplificada.

### Horizonte 4: Distribuicao e Escala

- Exportacao/importacao de flow com assets.
- Catalogo local e empacotamento simples.
- Caminho para distribuicao, compartilhamento e colaboracao.

## Ordem Recomendada

1. Validacao pre-run
2. Testar seletor/node
3. Historico de execucoes
4. Criar node a partir do Mira
5. Exportar/importar flow com assets

## Backlog Ativo

### Alta prioridade

- Registrar e manter este roadmap atualizado no repositorio.
- Implementar validacao pre-run no core e expor resultados de forma compativel com a UI.
- Preparar base de historico de execucoes persistido e observabilidade local.

### Media prioridade

- Implementar teste rapido de node/seletor na infraestrutura existente.
- Implementar criacao guiada de nodes a partir do Mira.
- Implementar exportacao/importacao de flow com assets e manifesto simples.

## Status Atual

- Em andamento: documentacao do roadmap e primeira entrega de validacao pre-run.
- Proxima integracao apos estabilizar os testes: bridge/UI para exibir resultados ricos de validacao.
