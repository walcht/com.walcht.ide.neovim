local function restart_roslyn_ls()
	local ls_name = "roslyn_ls"
	if vim.lsp.config[ls_name] == nil then
		vim.notify(("Invalid server ls_name '%s'"):format(ls_name))
	else
		vim.lsp.enable(ls_name, false)

		-- update Roslyn LS diagnostics scope settings
		vim.lsp.config[ls_name].settings["csharp|background_analysis"].dotnet_analyzer_diagnostics_scope =
			_G.nvim_unity_analyzer_diagnostic_scope
		vim.lsp.config[ls_name].settings["csharp|background_analysis"].dotnet_compiler_diagnostics_scope =
			_G.nvim_unity_compiler_diagnostic_scope

		vim.iter(vim.lsp.get_clients({ ls_name = ls_name })):each(
			---@param client vim.lsp.Client
			function(client)
				client:stop(true)
			end
		)
	end

	---@diagnostic disable-next-line: undefined-field
	local timer = assert(vim.uv.new_timer())
	timer:start(500, 0, function()
		vim.schedule_wrap(vim.lsp.enable)(ls_name)
	end)
end

restart_roslyn_ls()
