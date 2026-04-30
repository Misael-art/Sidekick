# MARKETPLACE_EVALUATION.md

## Decisao Atual

Marketplace remoto e **viavel**, mas nao deve ser liberado como download/execucao irrestrita nesta release.

Motivo: uma automacao Sidekick pode clicar, digitar, ler arquivos, executar comandos de console e interagir com apps reais. Portanto, um marketplace precisa ser tratado como cadeia de distribuicao de software, nao como simples galeria de JSON.

## Caminho Seguro Recomendado

### Fase 1 - Catalogo Local Curado

- usar `flows/*.json` como recipes oficiais
- validar todo recipe com `FlowValidator`
- importar como copia editavel do usuario
- nunca armar automaticamente apos importacao

Status: base operacional ja existe pelos seed flows oficiais.

### Fase 2 - Marketplace Remoto Com Manifesto

Formato minimo recomendado:

```json
{
  "schemaVersion": 1,
  "version": "2026.04",
  "publisher": "Sidekick",
  "items": [
    {
      "id": "popup-auto-confirm",
      "name": "Popup Auto Confirm",
      "description": "Waits for a popup and clicks the confirm button.",
      "flowUrl": "https://example.com/flows/popup-auto-confirm.json",
      "sha256": "..."
    }
  ]
}
```

Regras obrigatorias:

- aceitar apenas HTTPS
- limitar tamanho do manifesto e do flow
- validar `schemaVersion`
- validar hash `sha256`
- validar JSON contra o schema de flow
- rodar `FlowValidator` antes de importar
- mostrar nodes perigosos antes da importacao, especialmente:
  - `action.consoleCommand`
  - `action.deleteFile`
  - `action.killProcess`
  - `action.httpRequest`
  - qualquer node futuro de upload/envio externo
- importar desarmado
- exigir revisao do usuario antes de `Run Now` ou `Arm`

### Fase 3 - Assinatura E Reputacao

Antes de um marketplace publico:

- assinatura do manifesto
- assinatura ou hash de cada flow
- publisher verificado
- versao minima/maxima compativel do Sidekick
- changelog por automacao
- aviso de capacidades sensiveis
- politica de update/rollback

## Recomendacao De Produto

Para a proxima entrega, implementar primeiro uma aba **Recipes** local com busca, preview, tags, riscos e botao **Importar copia**.

Depois disso, adicionar **Importar de URL** apenas com as protecoes acima. Nao liberar execucao automatica de automacoes baixadas da internet.
