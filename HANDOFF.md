# FMU — Handoff para o dev Unity

Este pacote traz uma animação feita no **Figma Motion** já convertida para **UI nativa
da Unity** (hierarquia real + `AnimationClip`), mais o pipeline que gera isso a partir
de um arquivo `.fmu`. Nada de vídeo/Lottie: são GameObjects e curvas nativas.

## O que tem no pacote

```
Editor/
  FmuImporter.cs      ScriptedImporter: .fmu -> prefab + AnimationClip (editor-only)
  FmuModel.cs         DTOs do formato .fmu
Runtime/
  FmuPlayer.cs        toca o clip via PlayableGraph (sem AnimatorController)
  FmuShapeGraphic.cs  UI Graphic que desenha elipse / retângulo-arredondado
  FmuSDF.shader       shader SDF (bordas nítidas em qualquer escala — bom p/ VR)
listening-indicator-1.fmu   a animação exportada do Figma
SCHEMA.md             especificação do formato .fmu
```

## Dependência (obrigatória)

- **Newtonsoft Json** — Package Manager → *Add package by name* →
  `com.unity.nuget.newtonsoft-json`. Sem isso o importer não compila.
- Unity 2021.3+ (testado em Unity 6 / uGUI). Não usa TextMeshPro nem XR.

## Como usar a animação

1. Importe o pacote (ou copie as pastas para `Assets/`) **com o Newtonsoft já instalado**.
2. O `listening-indicator-1.fmu` aparece no Project como um **prefab expansível** — abrindo,
   há o `listening-indicator-1_clip` (AnimationClip) dentro.
3. Arraste o `.fmu` para **dentro de um Canvas** (é UI — na raiz da cena não renderiza).
4. **Play.** O `FmuPlayer` toca o clip em loop. Sem AnimatorController: ele dirige um
   `AnimationClipPlayable` com clock manual (loop sobre a `duration` da timeline do Figma).

Ajustes no `FmuPlayer` (no root do prefab): `loop`, `speed`, `duration`.

## Como funciona (resumo técnico)

- **Coordenadas**: cada nó usa anchor+pivot top-left e o `y` é negado, espelhando o espaço
  top-left/y-down do Figma. `HEIGHT` cresce para baixo (pivot no topo) como no Figma.
- **Easing**: cada segmento cubic-bezier é **baked** — amostrado no frame rate do clip
  (`sampleRate`, default 60) e gravado como keyframes lineares. Bate com o Figma sem
  aproximar tangentes de Hermite.
- **Nomes únicos**: irmãos com nome repetido (ex.: três `Rectangle`) são renomeados
  (`Rectangle`, `Rectangle-2`, ...) para os paths das curvas não colidirem.
- **Formas**: `FmuShapeGraphic` + `FmuSDF.shader` desenham as formas por distance field
  (nítidas em qualquer zoom). O componente liga sozinho o canal `TexCoord1` do Canvas.

## Importar novas animações do Figma

1. Designer roda o plugin **FMU Exporter** no Figma (pasta `figma-plugin/` do repositório),
   seleciona o frame animado e exporta o `.fmu`.
2. Solta o `.fmu` em `Assets/`. Vira prefab + clip automaticamente.

## Limites atuais (POC)

Suportado: `OPACITY`, `HEIGHT`, `WIDTH`, `TRANSLATION_X/Y`, e (não testado ainda)
`ROTATION`, `SCALE`. Formas: retângulo-arredondado e elipse/círculo.

Ainda **não** suportado: cor/gradiente animados, springs, morph de path vetorial, efeitos
(blur/shadow/shader), texto animado. Cor e spring precisam de um caso de teste para
implementar (o baking de curva já está pronto para receber spring).

Detalhes do formato em [SCHEMA.md](SCHEMA.md).
