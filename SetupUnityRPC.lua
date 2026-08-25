-- This script is sourced when Unity connects to the nvim RPC channel.
-- Here we can setup autocmd to trigger assets reload, create usefull user commands, etc.
local chan_name = "NvimUnityRpc"

if vim.g.unity_auto_sync == nil then
	vim.g.unity_auto_sync = true
end

if vim.g.unity_auto_sync_on_create == nil then
	vim.g.unity_auto_sync_on_create = true
end

if vim.g.unity_auto_sync_on_modify == nil then
	vim.g.unity_auto_sync_on_modify = true
end

-- find Unity channel id
local unity_chan_id = nil
for _, chan in ipairs(vim.api.nvim_list_chans()) do
	-- should match NeovimRpcClient.NeovimRpcClientName
	if chan.client and chan.client.name == chan_name then
		unity_chan_id = chan.id
		break
	end
end

if not unity_chan_id then
	return
end

-- save the id for later use
vim.g.unity_rpc_channel = unity_chan_id

local function cleanup_unity_integration()
	vim.g.unity_rpc_channel = nil

	vim.api.nvim_clear_autocmds({ group = "UnityIntegrationGroup" })
	vim.api.nvim_del_user_command("UnitySync")

	vim.notify("Unity RPC disconnected. Cleared commands and autocmds.", vim.log.levels.INFO)
end

local function check_channel()
	local chan_id = vim.g.unity_rpc_channel

	if not chan_id then
		return nil
	end

	local chan_info = vim.api.nvim_get_chan_info(chan_id)
	if not chan_info.id then
		cleanup_unity_integration()
		return nil
	end

	return chan_id
end

vim.api.nvim_create_user_command("UnitySync", function()
	local chan_id = check_channel()

	if not chan_id then
		return
	end

	vim.rpcnotify(chan_id, "UnitySyncAll")
end, { desc = "Manually synchronize project and reimport assets" })

local unity_group = vim.api.nvim_create_augroup("UnityIntegrationGroup", { clear = true })

-- handle file saves
vim.api.nvim_create_autocmd("BufWritePost", {
	group = unity_group,
	callback = function(opts)
		if not vim.g.unity_auto_sync then
			return
		end

		local chan_id = check_channel()
		if not chan_id then
			return
		end

		local filepath = opts.match
		-- check if .meta file exists
		local is_new = vim.uv.fs_stat(filepath .. ".meta") == nil

		if is_new then
			if vim.g.unity_auto_sync_on_create then
				vim.rpcnotify(chan_id, "UnityAssetCreated", filepath)
			end
		else
			if vim.g.unity_auto_sync_on_modify then
				vim.rpcnotify(chan_id, "UnityAssetChanged", filepath)
			end
		end
	end,
})
