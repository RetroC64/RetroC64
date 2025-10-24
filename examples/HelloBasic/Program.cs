using RetroC64.App;

return await C64AppBuilder.Run<HelloBasic>(args);

class HelloBasic : C64AppBasic
{
    protected override void Initialize(C64AppInitializeContext context)
    {
        Text = """
               10 X = 1
               20 PRINT "HELLO, WORLD" X
               30 REM X = X + 1
               40 REM GOTO 20
               """;
    }
}