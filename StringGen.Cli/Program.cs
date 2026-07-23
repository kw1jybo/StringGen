using StringGen.Cli;

return await CommandFactory.Build().Parse(args).InvokeAsync();
