# NG03 — evidências automatizadas (12/11/2026)

Implementado, validação runtime pendente. Base `d1b928a28752bc46487f8af920dfaa7ac7bde8fe` + diff local. Unity 6000.6.0f1, protocolo inalterado.

- Unity/C#: sem erros/avisos CS no log; baseline EditMode 1127/1127 Passed, zero failed/skipped. Casos com e sem arco aprovados.
- `python -m unittest discover -s Tools/RoomDirectory -p "test_*.py"`: 16 Passed.
- `python -m unittest discover -s Tools/Ci -p "test_*.py"`: 218 Passed (inclui alinhamento real de protocolo).
- UTF-8 estrito/BOM dos 3 arquivos C# conferidos. Nenhum private field foi adicionado.
- Runtime offline, host, dedicado e dois clientes: NÃO executados. Falta binário atualizado e fixture de morte Fácil; smoke co-op atual configura Hard. Sem aceite de apresentação ou controle em partida.
- Nenhum build/deploy. Logs e XML permanecem em `Logs/PlaytestNormalGame20261112/`. Efeitos gerados do Editor foram arquivados e os dois assets restantes restaurados ao HEAD inicial (duas fontes já haviam sido restauradas pela execução). Nenhuma remoção de arquivo.

```json
{
  "sourceHead": "d1b928a28752bc46487f8af920dfaa7ac7bde8fe",
  "changedCSharp": [
    {
      "path": "Assets/Scripts/Netcode/NetworkRevengeReturn.cs",
      "sha256": "e78057cf594fea8242874849d519a11b24a9eeaf3ed24c40afb76e4a41678780"
    },
    {
      "path": "Assets/Scripts/PlayerMovimentation/MovementController.cs",
      "sha256": "67957e7b1d529566bf9d315f748f4545985cb572bb2fee15ee475ed394a92615"
    },
    {
      "path": "Assets/Tests/EditMode/Editor/OnlineRoomRevengeCartTests.cs",
      "sha256": "d70fbb057bc39a5caaa6347aab77693a4d4c505efd4170d97f4045a1d172ea58"
    }
  ],
  "python": {
    "Tools/RoomDirectory": {
      "tests": 16,
      "result": "Passed"
    },
    "Tools/Ci": {
      "tests": 218,
      "result": "Passed"
    }
  },
  "runtime": "Pending: no fresh Development builds or Easy respawn fixture; no offline/host/dedicated-owner-observer acceptance",
  "unity": {
    "result": "Passed",
    "total": "1127",
    "passed": "1127",
    "failed": "0",
    "skipped": "0",
    "csharpCompilerErrors": 0,
    "csharpCompilerWarnings": 0
  },
  "artifacts": {
    "editmode.xml": "f6db43010f9f1ec8a72dd6e79395f0a2b59e8acf1064d07182794f6f4b5848d6",
    "unity.log": "27464664b22da624a6b2cd1acb47e5ed80c7ae573f593832183ed7c79dc8d6f6",
    "cleanup-manifest.json": "fd79e617d345a554afa0e8e1f8f1003a82dc7aacdd84906b431e03aeeb0a4073"
  },
  "finalWhitespaceOnlyTestFormatting": true,
  "finalCSharp": [
    {
      "path": "Assets/Scripts/Netcode/NetworkRevengeReturn.cs",
      "sha256": "e78057cf594fea8242874849d519a11b24a9eeaf3ed24c40afb76e4a41678780"
    },
    {
      "path": "Assets/Scripts/PlayerMovimentation/MovementController.cs",
      "sha256": "67957e7b1d529566bf9d315f748f4545985cb572bb2fee15ee475ed394a92615"
    },
    {
      "path": "Assets/Tests/EditMode/Editor/OnlineRoomRevengeCartTests.cs",
      "sha256": "5a8928cc7741b3f51162b333b4d36cb5b1b7d7e1bda07ff159b73d736921dcb7"
    }
  ]
}
```

Manifesto dos assets restaurados:

```json
[
  {
    "path": "Assets/Resources/Fonts/PressStart2P-Regular SDF.asset",
    "generatedSha256": "7f09a03c7437592134b43da7eff9f22db0b2bc5d2293ff8091244b0cba778952",
    "restoredHeadSha256": "418a2fdc237743e061a4eef7acfe2d569cded05fc1fd536ddd114d6b670cb08c",
    "archive": "Logs\\PlaytestNormalGame20261112\\editor-side-effects\\Assets\\Resources\\Fonts\\PressStart2P-Regular SDF.asset",
    "restoredCheckoutSha256": "c00cf0ed1753b85ac6542e77d3e0144b78bc734b6ba0e96ea588fba8aad19d30",
    "restore": "HEAD via git restore --worktree, applying checkout line endings; git diff empty"
  },
  {
    "path": "ProjectSettings/Physics2DSettings.asset",
    "generatedSha256": "af505fe248c817d70da84bb2fa8336424d34fc6ef6690c03bf50546c626697b1",
    "restoredHeadSha256": "a4e6f2da6043e3a1e1dcdb738a5f020fe0dc629317ef76822ca951939c266992",
    "archive": "Logs\\PlaytestNormalGame20261112\\editor-side-effects\\ProjectSettings\\Physics2DSettings.asset",
    "restoredCheckoutSha256": "725aec26214e80098fe6f6bd19b8a07f1a62ce3f5c4d49b4ad3de67be0e57e13",
    "restore": "HEAD via git restore --worktree, applying checkout line endings; git diff empty"
  }
]
```

Depois dos testes houve somente ajuste de indentação do bloco withArc no teste, sem mudança de comportamento. Hashes compilados e finais preservados separadamente.


## Validação runtime — B09, 13/11/2026

- Build Windows a80782c5ffe4dcb9a8350b3b53fc55dad5adceedfdb77ac4c2254fb603ebe34c; Linux a6bf72c9883ea3546e58400a5440d21280781a383b5c2a112d7a525434b14b43; protocolo74. source.patch/identity.json em Build/NormalPlaytest/Batch09.
- easy-01:180s Linux dedicado real +2 Windows renderizados/KCP via bridge SSH isolado.4 mortes nativas/4 respawns; cada cliente observou4 retornos e seu dono recuperou movimento em2 ciclos (P1 e P2). Pausa/resume com vivos, rejeição durante morte simultânea, reload/novo stream e segunda pausa concluídos sem exceções.
- Logs, result.json, diagnostics-manifest.json com3 capturas completas/SHA256 e PNGs no diretório easy-01. Quatro retornos por cliente incluem observação do outro ator e morte simultânea. clientMovement=[2,2], não deduzir controle apenas por vida positiva.
- Offline B06 já executou4 mortes/respawns, mas medição antiga de controle compartilhava origem entre atores. Repetir offline com B09/B10 fixture por ator. Host ainda pendente; não chamar NG03 integralmente validado antes dessas coberturas.


## Complemento offline B09 e host B13

- Offline B09 easy-offline-01 concluiu4 mortes/4 retornos,50 PNGs, com medição por ator e recuperação de movimento dos2 jogadores. A pendência de repetição offline do parágrafo anterior foi suprida.
- Host B13 host-easy-01: host real+1 cliente remoto Windows visíveis, controles de cada dono pelo input nativo. Resultado:4 mortes/4 retornos, localMovement2/remoteMovement2, pausa vivos/rejeição durante morte, teardownNativeStopHost clientFalse/serverFalse. Sem InvalidOperationException/NullReferenceException/MissingReferenceException/Unknown message. diagnostics-manifest.json preserva logs de movimento host/client eSHA256.
- DLL Windows816901d09642bf0d04daf4d1b2267c3d947c3db7a67b389379d21e1114a85103. Protocolo74, baseline1158/1158 Passed; Python16+218 Passed. Identity/source.patch imutáveis em Build/NormalPlaytest/Batch13.
- B11 host reprovado por CoopStageStatus50207 sem handler local; B12 host avançou até pausa mas desconectou por OnlinePauseMessage32046. Foram corrigidos envio remoto e consumo de broadcasts compartilhados no host sem reaplicação de efeitos. Essas tentativas permanecem arquivadas como falhas, sem culpar fechamento de janelas pelo usuário.
- Atenção: o fixture B09/B13 chamava TakeDamage(false) no ciclo1. Isso cobre dano sem explosão, NÃO morte por buraco. Fonte B14 agora chama KillByHole real e verifica actor.isDead; ainda não compilada/executada. Manter NG03 em validação até essa cobertura separada, sem reabrir diagnóstico inicial de HasEasyNormalGameRespawn.


## Aceite B14Retry01 — buraco, papéis e controle

Baseline Unity 1158/1158 e Python 16+218 aprovados. Windows SHA78d6b8c90561e09780018e362c7b8becfbabbf4c8c6e1e6ae26b906cdd6f6aa5; Linux SHA9c55296de003a52f23cab461ada49ebb62de580d38b36a284341e1a88fb8c29f; protocolo74. Sem publicação.

Offline easy-hole-offline-01: quatro retornos, movimento recuperado duas vezes por jogador, conclusão/quit0,50PNGs. Host-easy-hole-01: quatro retornos, controle local2/remote2, StopHost limpo e nenhum erro. Linux+2clients easy-hole-01: quatro mortes e quatro retornos autoritativos, incluindo KillByHole, ambos owners moveram2vezes após voltar; pausas/reload/teardown completos. Observador viu5/4 logs brutos de revive (observações de apresentação, não mortes únicas); contagens autoritativas e retomada dos dois controles são os critérios. Resultados e diagnósticos arquivados em Build/NormalPlaytest/Batch14Retry01. NG03 validado para o relato, sem alegar que cada tipo futuro de morte é coberto por este cenário.
