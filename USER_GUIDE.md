# Ajudante — Guia do Usuario

> Automacao visual para Windows. Crie automacoes conectando blocos — sem escrever codigo.

---

## 1. Primeiros Passos

### 1.1 Requisitos do Sistema
- Windows 10/11 (64-bit)
- .NET 8 Runtime (incluso na versao standalone)
- WebView2 Runtime (pre-instalado no Windows 11; baixe em https://developer.microsoft.com/en-us/microsoft-edge/webview2/ se necessario)

### 1.2 Instalacao
1. Extraia o arquivo `Ajudante.zip` em qualquer pasta
2. Execute `Ajudante.exe`
3. O icone do Ajudante aparecera na bandeja do sistema (system tray)

### 1.3 Interface
A interface e dividida em:

```
+-------+------------------------------+-----------+
|       |                              |           |
|Palette|        Canvas (area          | Property  |
| (nos) |       de trabalho)           |  Panel    |
|       |                              |(propried.)|
|       |                              |           |
+-------+------------------------------+-----------+
|              Toolbar (topo)                      |
+--------------------------------------------------+
|           Status Bar (rodape)                    |
+--------------------------------------------------+
```

- **Toolbar (topo):** Novo flow, Salvar, Carregar, Play/Stop, Mira, Snip, nome do flow
- **Palette (esquerda):** Lista de nodes disponiveis, agrupados por categoria
- **Canvas (centro):** Area onde voce arrasta e conecta os nodes
- **Property Panel (direita):** Configuracoes do node selecionado
- **Status Bar (rodape):** Status de execucao e logs

---

## 2. Conceitos Basicos

### 2.1 O que e um Flow?
Um **Flow** e uma automacao completa. Ele consiste em nodes (blocos) conectados por fios que definem a ordem de execucao.

### 2.2 Tipos de Nodes
Existem tres categorias de nodes, identificadas por cor:

| Categoria | Cor | Funcao |
|-----------|-----|--------|
| **Trigger** (Gatilho) | Vermelho | Inicia o flow quando um evento ocorre |
| **Logic** (Logica) | Amarelo | Controla o fluxo: condicoes, loops, variaveis, delays |
| **Action** (Acao) | Verde | Executa uma acao: mover mouse, digitar texto, abrir programa |

### 2.3 Portas (Ports)
Cada node tem portas de entrada (esquerda) e saida (direita). Conecte a porta de saida de um node a porta de entrada do proximo arrastando um fio entre elas.

- **Triggers** nao tem porta de entrada (sao o inicio do flow)
- Nodes de **branching** (If/Else, Compare Text) tem multiplas portas de saida (True/False, Match/NoMatch)

### 2.4 Variaveis
Flows podem usar variaveis para armazenar dados entre nodes. Use a sintaxe `{{nomeDaVariavel}}` em campos de texto para referenciar variaveis.

---

## 3. Criando seu Primeiro Flow

### Exemplo: Atalho que digita um texto

1. **Arraste** o node "Hotkey Trigger" da palette para o canvas
2. No Property Panel, configure a tecla (ex: `F9`) e o modificador (`Ctrl`)
3. **Arraste** o node "Keyboard Type" para o canvas
4. Configure o texto a ser digitado (ex: `Ola, mundo!`)
5. **Conecte** a porta "Triggered" do Hotkey a porta "In" do Keyboard Type
6. Clique em **Play** na toolbar
7. Pressione `Ctrl+F9` em qualquer lugar — o texto sera digitado automaticamente

### Exemplo: Monitor de pasta com som

1. Arraste "File System Trigger" → configure: pasta = `C:\Downloads`, filtro = `*.pdf`, evento = `Created`
2. Arraste "Play Sound" → configure o arquivo de som
3. Conecte `Triggered` → `In`
4. Play! Sempre que um PDF for criado na pasta, o som tocara

---

## 4. Referencia Completa de Nodes

### 4.1 Triggers (Gatilhos)

#### Hotkey Trigger
**Quando usar:** Iniciar automacao com atalho de teclado.
| Propriedade | Descricao |
|-------------|-----------|
| Key | Tecla principal (A-Z, F1-F12, etc.) |
| Modifiers | Modificador: None, Ctrl, Shift, Alt, Win |

#### Image Detected Trigger
**Quando usar:** Reagir quando uma imagem aparece na tela.
| Propriedade | Descricao |
|-------------|-----------|
| Template Image | Imagem de referencia (use Snip para capturar) |
| Threshold | Precisao da correspondencia (0.0 a 1.0, padrao: 0.8) |
| Interval | Intervalo de verificacao em ms (padrao: 1000) |

**Saidas:** `x`, `y` — coordenadas onde a imagem foi encontrada.

#### Pixel Change Trigger
**Quando usar:** Reagir quando a cor de um pixel muda.
| Propriedade | Descricao |
|-------------|-----------|
| X, Y | Coordenadas do pixel |
| Interval | Intervalo de verificacao em ms (padrao: 500) |

**Saidas:** `oldColor`, `newColor`, `x`, `y`.

#### Window Event Trigger
**Quando usar:** Reagir a abertura, fechamento ou foco de janelas.
| Propriedade | Descricao |
|-------------|-----------|
| Event Type | Opened, Closed, ou Focused |
| Window Title | Titulo da janela (filtro) |

**Saida:** `windowTitle`.

#### File System Trigger
**Quando usar:** Reagir a mudancas em arquivos/pastas.
| Propriedade | Descricao |
|-------------|-----------|
| Path | Caminho da pasta a monitorar |
| Filter | Filtro de arquivo (ex: `*.txt`, `*.*`) |
| Event Type | Created, Changed, ou Deleted |

**Saidas:** `filePath`, `fileName`.

---

### 4.2 Logic (Logica)

#### If / Else
**Quando usar:** Tomar decisoes baseadas em condicoes.
| Propriedade | Descricao |
|-------------|-----------|
| Condition Type | VariableEquals, VariableContains, VariableGreaterThan, PixelColorIs |
| Variable Name | Nome da variavel a verificar |
| Compare Value | Valor de comparacao |

**Saidas:** `True` ou `False`.

#### Delay
**Quando usar:** Pausar a execucao.
| Propriedade | Descricao |
|-------------|-----------|
| Milliseconds | Duracao da pausa (padrao: 1000) |

#### Compare Text
**Quando usar:** Comparar dois textos.
| Propriedade | Descricao |
|-------------|-----------|
| Text 1, Text 2 | Textos a comparar (suportam `{{variaveis}}`) |
| Comparison | Equals, Contains, StartsWith, EndsWith |

**Saidas:** `Match` ou `No Match`.

#### Loop
**Quando usar:** Repetir acoes N vezes.
| Propriedade | Descricao |
|-------------|-----------|
| Iterations | Numero de repeticoes (padrao: 5) |
| Delay Between | Pausa entre repeticoes em ms (padrao: 0) |

**Saidas:** `Body` (cada iteracao), `Done` (apos terminar).
**Variaveis automaticas:** `loopIndex` (0-based), `loopIteration` (1-based), `loopCount`.

#### Set Variable
**Quando usar:** Armazenar um valor em uma variavel.
| Propriedade | Descricao |
|-------------|-----------|
| Variable Name | Nome da variavel |
| Value | Valor (suporta `{{variaveis}}`) |

#### Get Variable
**Quando usar:** Ler o valor de uma variavel.
| Propriedade | Descricao |
|-------------|-----------|
| Variable Name | Nome da variavel |

**Saida de dados:** `value`.

---

### 4.3 Actions (Acoes)

#### Mouse Click
**Quando usar:** Clicar em uma posicao da tela.
| Propriedade | Descricao |
|-------------|-----------|
| X, Y | Coordenadas do clique |
| Button | Left, Right, Middle |
| Click Type | Single, Double |

#### Mouse Move
**Quando usar:** Mover o cursor.
| Propriedade | Descricao |
|-------------|-----------|
| X, Y | Posicao de destino |

#### Mouse Drag
**Quando usar:** Arrastar de um ponto a outro.
| Propriedade | Descricao |
|-------------|-----------|
| From X, From Y | Ponto de origem |
| To X, To Y | Ponto de destino |

#### Keyboard Type
**Quando usar:** Digitar texto.
| Propriedade | Descricao |
|-------------|-----------|
| Text | Texto a digitar (suporta `{{variaveis}}`) |
| Delay Between Keys | Pausa entre cada tecla em ms (padrao: 0) |

#### Keyboard Press
**Quando usar:** Pressionar uma tecla especifica (atalho).
| Propriedade | Descricao |
|-------------|-----------|
| Key | Tecla (Return, Tab, Escape, A-Z, F1-F12, etc.) |
| Modifiers | None, Ctrl, Shift, Alt |

#### Open Program
**Quando usar:** Abrir um programa ou arquivo.
| Propriedade | Descricao |
|-------------|-----------|
| Path | Caminho do executavel |
| Arguments | Argumentos de linha de comando |

#### Kill Process
**Quando usar:** Encerrar um processo.
| Propriedade | Descricao |
|-------------|-----------|
| Process Name | Nome do processo (ex: `notepad`) |

#### Play Sound
**Quando usar:** Reproduzir um som de notificacao.
| Propriedade | Descricao |
|-------------|-----------|
| Sound File | Caminho do arquivo de audio (.wav) |

#### Delete File
**Quando usar:** Apagar um arquivo.
| Propriedade | Descricao |
|-------------|-----------|
| File Path | Caminho completo do arquivo |

---

## 5. Ferramentas Especiais

### 5.1 Mira (Inspetor de Elementos)
O modo **Mira** permite inspecionar elementos de interface de qualquer janela.

1. Clique no botao **Mira** na toolbar
2. Uma sobreposicao transparente cobre toda a tela
3. Mova o cursor sobre qualquer elemento — ele sera destacado em tempo real
4. Clique para capturar as informacoes do elemento (nome, classe, tipo, coordenadas)
5. As informacoes capturadas podem ser usadas para configurar nodes

**Uso tipico:** Descobrir coordenadas precisas de botoes, campos de texto e outros controles.

### 5.2 Snip (Captura de Regiao)
O modo **Snip** permite selecionar uma regiao da tela.

1. Clique no botao **Snip** na toolbar
2. Uma sobreposicao escurecida cobre a tela
3. Clique e arraste para selecionar a regiao desejada
4. A imagem capturada e as coordenadas ficam disponiveis para uso em nodes

**Uso tipico:** Capturar templates para o Image Detected Trigger.

---

## 6. Dicas e Boas Praticas

### Organizacao
- De nomes descritivos aos seus flows
- Use variaveis para valores que podem mudar (caminhos, textos)
- Agrupe nodes relacionados proximos no canvas

### Performance
- Use triggers baseados em eventos (Hotkey, Window Event, File System) em vez de polling quando possivel
- Para Image Detected Trigger, aumente o intervalo se a verificacao nao precisa ser instantanea
- Adicione Delay nodes entre acoes rapidas para dar tempo ao sistema

### Depuracao
- Observe a Status Bar para logs de execucao em tempo real
- Nodes destacados em vermelho indicam erro
- Nodes destacados em azul estao em execucao

### Variaveis
- Use `{{nomeVar}}` em qualquer campo de texto para interpolar variaveis
- Triggers podem definir variaveis automaticamente (ex: `filePath` do File System Trigger)
- Loop define `loopIndex`, `loopIteration`, `loopCount` automaticamente

---

## 7. Plugins

O Ajudante suporta nodes customizados via plugins.

### Instalando um Plugin
1. Copie o arquivo `.dll` do plugin para: `%AppData%\Ajudante\plugins\`
2. Reinicie o Ajudante
3. Os novos nodes aparecerao automaticamente na palette

### Criando um Plugin
Um plugin e uma DLL .NET 8 que implementa a interface `INode`:

1. Crie um projeto .NET 8 class library
2. Adicione referencia ao `Ajudante.Core.dll`
3. Crie classes que implementam `IActionNode`, `ILogicNode`, ou `ITriggerNode`
4. Decore cada classe com `[NodeInfo(TypeId = "...", DisplayName = "...", Category = ...)]`
5. Compile e copie a DLL para a pasta de plugins

---

## 8. Atalhos de Teclado

| Atalho | Acao |
|--------|------|
| Ctrl+N | Novo flow |
| Ctrl+S | Salvar flow |
| Ctrl+O | Carregar flow |
| Delete | Remover node/conexao selecionada |
| Ctrl+Z | Desfazer |
| Scroll | Zoom in/out no canvas |
| Click+Drag | Mover o canvas |

---

## 9. Solucao de Problemas

| Problema | Solucao |
|----------|---------|
| App nao inicia | Verifique se o WebView2 Runtime esta instalado |
| Nodes nao aparecem na palette | Reinicie o app — as definicoes sao carregadas na inicializacao |
| Flow nao executa | Verifique se ha um trigger conectado e clique em Play |
| Hotkey nao funciona | Certifique-se que o flow esta em execucao (Play ativo) |
| Image match falha | Tente reduzir o Threshold (ex: 0.7) ou recapture o template |
| Mouse clica no lugar errado | Use Mira para verificar coordenadas; resolucoes de monitor diferentes podem afetar |

---

## 10. Localizacao de Arquivos

| O que | Onde |
|-------|------|
| Flows salvos | `%AppData%\Ajudante\flows\` |
| Logs de erro | `%AppData%\Ajudante\logs\` |
| Plugins | `%AppData%\Ajudante\plugins\` |

Para abrir a pasta AppData: pressione `Win+R`, digite `%AppData%\Ajudante` e pressione Enter.
