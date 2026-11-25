// Los mensajes llevan tildes y eñes: que la consola los reciba en UTF-8.
Console.OutputEncoding = System.Text.Encoding.UTF8;
return CardLint.Cli.Programa.Ejecutar(args, Console.Out, Console.Error);
