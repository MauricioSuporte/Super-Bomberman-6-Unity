#if UNITY_EDITOR
// Keep test bodies beside runtime internal helpers, and expose the inherited
// cases from the Editor test assembly so Unity can discover and execute them.
[NUnit.Framework.TestFixture]
public sealed class BattleModeComEditorTests : BattleModeComEditModeTests { }
#endif
