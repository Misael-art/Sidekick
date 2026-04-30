# Sidekick - Guia do Usuario

> Automacao visual para Windows. Monte flows conectando blocos, sem escrever codigo.

---

## 1. Primeiros passos

### 1.1 Requisitos
- Windows 10/11 (64-bit)
- .NET 8 Runtime na versao framework-dependent, ou nada extra na versao standalone
- WebView2 Runtime, quando necessario

### 1.2 Instalacao
1. Extraia `Sidekick.zip` em qualquer pasta.
2. Execute `Sidekick.exe`.
3. O icone do Sidekick aparecera na bandeja do sistema.

### 1.3 Estrutura da interface
- **Toolbar:** novo flow, salvar, carregar, Run Now, Arm/Disarm, Stop, Mira, Snip e nome do flow.
- **Palette:** coluna retratil com nodes agrupados por categoria.
- **Canvas:** area onde o flow e montado. Clique com o botao direito para abrir o menu rapido de nodes e acoes baseadas na ultima captura do Mira/Snip.
- **Property Panel:** configuracoes do node selecionado.
- **Status Bar:** estado do runtime, fila, flows armados e logs.

---

## 2. Conceitos basicos

### 2.1 O que e um flow
Um flow e uma automacao completa formada por nodes conectados por fios.

### 2.2 Categorias de nodes
| Categoria | Cor | Funcao |
|-----------|-----|--------|
| Trigger | Vermelho | Inicia o flow quando um evento ocorre |
| Logic | Amarelo | Controla condicoes, loops, variaveis e delays |
| Action | Verde | Executa uma acao concreta |

### 2.3 Ports
- Triggers nao possuem entrada.
- A maioria dos nodes usa `In` e `Out`.
- Nodes de decisao podem expor saidas como `True/False` ou `Match/NoMatch`.

### 2.4 Variaveis
Use `{{nomeDaVariavel}}` em campos de texto para interpolar valores do contexto.

### 2.5 Run Now vs Arm
O runtime do Sidekick separa claramente os conceitos:

- **Run Now:** executa o flow uma vez usando o snapshot atual do editor.
- **Arm:** deixa os triggers do flow ativos em background.
- **Disarm:** interrompe o monitoramento do flow.
- **Stop:** interrompe a execucao atual. Se existir fila pendente, voce pode parar apenas a execucao atual ou limpar toda a fila.

Regra pratica:

- Use **Run Now** para testes e execucoes pontuais.
- Use **Arm** para automacoes continuas baseadas em hotkey, arquivos, janelas, imagens ou pixels.

---

## 3. Seu primeiro flow

### Exemplo 1: hotkey que digita um texto
1. Arraste `Hotkey Trigger` para o canvas.
2. Configure a tecla, por exemplo `F9`, e o modificador `Ctrl`.
3. Arraste `Keyboard Type`.
4. Defina o texto, por exemplo `Ola, mundo!`.
5. Conecte `Triggered` -> `In`.
6. Clique em **Arm**.
7. Pressione `Ctrl+F9` em qualquer lugar para disparar a automacao.

### Exemplo 2: monitor de pasta com som
1. Arraste `File System Trigger` e configure a pasta, filtro e tipo de evento.
2. Arraste `Play Sound`.
3. Conecte `Triggered` -> `In`.
4. Clique em **Arm**.

### Exemplo 3: teste manual de um flow armado
1. Com o flow ainda aberto no editor, clique em **Run Now**.
2. O flow sera enfileirado para uma execucao imediata.
3. O fato de o flow estar armado ou nao nao altera o JSON salvo; o arm/disarm e estado de runtime.

### Exemplo 4: esperar um texto e clicar em um botao
1. Abra o app alvo.
2. Clique em **Mira** e capture o texto ou botao que quer automatizar.
3. Adicione `Desktop Wait Element` para esperar o texto aparecer.
4. Adicione `Desktop Click Element` para clicar no botao.
5. No **Property Panel**, use **Use Latest** ou **Browse Mira** para aplicar a captura ao node.
6. Clique em **Test Selector** na biblioteca do Mira quando quiser conferir se o alvo ainda e encontrado.
7. Use **Run Now** para testar e **Arm** quando o flow tiver um trigger continuo.

---

## 4. Referencia rapida de nodes

### 4.1 Triggers
- **Hotkey Trigger:** dispara com atalho global.
- **File System Trigger:** observa criacao, alteracao ou remocao de arquivos.
- **Window Event Trigger:** observa abertura, fechamento e foco de janelas.
- **Pixel Change Trigger:** monitora alteracao de cor em um ponto.
- **Image Detected Trigger:** procura uma imagem na tela.
- **Desktop Element Appeared:** dispara quando um elemento desktop aparece.
- **Desktop Text Changed:** dispara quando o texto de um elemento desktop muda.
- **Schedule Time:** dispara em um horario local fixo.
- **Interval:** dispara em intervalos recorrentes.
- **Process Event:** observa inicio/parada de processo.

### 4.2 Logic
- **If / Else:** desvia o fluxo com base em uma condicao.
- **Delay:** pausa a execucao.
- **Loop:** repete um bloco N vezes.
- **Set Variable / Get Variable:** grava e le variaveis.
- **Compare Text:** compara strings.
- **Cooldown:** evita repeticoes muito proximas na mesma execucao.

### 4.3 Actions
- **Mouse Click / Move / Drag**
- **Keyboard Type / Press**
- **Open Program**
- **Kill Process**
- **Wait Process**
- **Window Control**
- **Desktop Wait Element**
- **Desktop Click Element**
- **Desktop Read Element Text**
- **Click Image Match**
- **Play Sound**
- **Delete File**

---

## 5. Ferramentas especiais

### 5.1 Mira
Use o modo **Mira** para inspecionar elementos na tela e capturar metadados de interface.

Fluxo pratico atual:

1. Clique em **Mira** na toolbar e capture um elemento da interface.
2. Selecione um node desktop no canvas, como `Desktop Wait Element`, `Desktop Click Element` ou `Desktop Read Element Text`.
3. No **Property Panel**, use o bloco `Mira Selector`.
4. Escolha **Use Latest** para aplicar a captura mais recente, ou **Browse Mira** para selecionar um ativo salvo.
5. O Sidekick preenche `windowTitle`, `windowTitleMatch`, `processName`, `processPath`, `automationId`, `elementName` e `controlType` quando o node suporta esses campos.

O Mira mostra dados pensados para automacao:

- nome do elemento, janela, processo e caminho do executavel
- class name, automation id, control type e bounds absolutos/relativos
- cursor, cor sob o cursor, foco, enabled e visible
- estrategia sugerida e robustez do seletor (`forte`, `media` ou `fraca`)
- preview do ativo salvo e acao **Test Selector**

Dicas:

- Use **Browse Mira** quando quiser reutilizar um seletor salvo em varios flows.
- Use **Use Latest** quando estiver ajustando um flow rapidamente logo apos a inspecao.
- Se necessario, clique em **Clear** para limpar o binding e editar os campos manualmente.

### 5.2 Snip
Use **Snip** para capturar uma regiao da tela e reutilizar a imagem como template.

Fluxo pratico atual:

1. Capture a regiao com **Snip**.
2. Selecione um `Image Detected Trigger` ou `Click Image Match` no canvas.
3. No **Property Panel**, abra o seletor do campo de template.
4. Escolha o ativo salvo do `Snip` ou use a opcao de aplicar o mais recente.
5. Use `Image Detected Trigger` para monitorar em background ou `Click Image Match` para clicar uma imagem como fallback visual.

Limite atual: OCR ainda nao e recurso de produto. Quando UIAutomation nao encontra um app moderno/hibrido, use `Snip` + `Click Image Match` como fallback visual honesto.

### 5.3 Sample flows uteis
Os exemplos abaixo mostram o uso de ativos reutilizaveis no editor:

- `portfolio_snip_reuse_demo.json`: reaproveita um ativo de `Snip` em `Image Detected Trigger`.
- `portfolio_browser_mira_demo.json`: aplica um seletor do `Mira` em `Browser Wait Element` e `Browser Click`.
- `portfolio_browser_mira_text_demo.json`: aplica um seletor do `Mira` em `Browser Type` e `Browser Extract Text`.
- `recipe_desktop_automation.json`: captura/aplica seletor desktop e le texto.
- `recipe_wait_text_then_click.json`: espera texto e clica em um botao.
- `recipe_popup_auto_confirm.json`: clica automaticamente em um popup com cooldown/debounce/max repeat.
- `recipe_visual_fallback_click.json`: usa image matching quando selector nao basta.
- `recipe_scheduler_interval.json`: demonstra automacao por intervalo.
- `trae_auto_continue.json`: flow oficial para Trae, usando trigger de elemento desktop, foco de janela e click protegido contra click storm.

---

## 6. Runtime continuo

### 6.1 O que a status bar mostra
- flow atual do editor
- flow em execucao agora
- quantidade de flows armados
- profundidade da fila
- ultimo trigger, ultima execucao e ultimo erro por flow
- fases semanticas como `waiting for element`, `element matched`, `fallback visual active`, `cooldown active` e `click executed`

### 6.2 Fila e coalescencia
Quando o mesmo flow dispara varias vezes em pouco tempo, o Sidekick mantem no maximo:

- 1 execucao atual
- 1 execucao pendente para esse mesmo flow

Isso evita reentrancia descontrolada e deixa o runtime previsivel.

### 6.3 Stop
Se houver fila pendente, o botao **Stop** abre uma escolha local:

- parar apenas a execucao atual
- parar a execucao atual e limpar a fila

---

## 7. Plugins

O Sidekick suporta nodes customizados via plugins.

### 7.1 Instalacao de plugin
1. Copie a DLL para `%AppData%\\Sidekick\\plugins\\`.
2. Reinicie o Sidekick.
3. Os novos nodes aparecerao automaticamente na palette.

### 7.2 Compatibilidade
Se voce vier de uma versao antiga que usava `%AppData%\\Ajudante`, o Sidekick migra os dados automaticamente na primeira execucao e preserva a pasta antiga como legado.

---

## 8. Dicas praticas

- Prefira triggers baseados em evento quando possivel.
- Use `Delay` entre acoes muito rapidas para deixar a automacao mais confiavel.
- Observe os logs da status bar ao depurar.
- Se um flow usa trigger continuo, lembre-se de **armar** o flow; executar manualmente nao deixa watchers ativos.

---

## 9. Solucao de problemas

| Problema | Solucao |
|----------|---------|
| App nao inicia | Verifique se o WebView2 Runtime esta instalado |
| Nodes nao aparecem | Reinicie o app para recarregar definicoes |
| Flow nao executa | Use **Run Now** para teste pontual ou **Arm** para monitoramento continuo |
| Hotkey nao funciona | Confirme que o flow esta armado |
| Trigger de pasta nao reage | Verifique o caminho, o filtro e o tipo de evento |
| Image match falha | Reduza o threshold ou recapture o template |
| Desktop selector nao encontra o alvo | Use **Test Selector**, confira `processName`, `processPath`, `windowTitleMatch` e recapture com Mira |
| App moderno nao expoe controles por UIAutomation | Use `Snip` + `Click Image Match` como fallback visual; OCR ainda nao esta pronto |
| Flow dispara vezes demais | Configure `cooldownMs`, `debounceMs` e `maxRepeat` no trigger desktop |

---

## 10. Onde ficam os arquivos

| O que | Onde |
|-------|------|
| Flows | `%AppData%\\Sidekick\\flows\\` |
| Logs | `%AppData%\\Sidekick\\logs\\` |
| Plugins | `%AppData%\\Sidekick\\plugins\\` |

Para abrir a pasta principal, pressione `Win+R`, digite `%AppData%\\Sidekick` e pressione Enter.
