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

-- find the channel id of the Unity
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

vim.g.unity_rpc_channel = unity_chan_id

local function check_channel()
	local chan_id = vim.g.unity_rpc_channel

	if not chan_id then
		return nil
	end

	local chan_info = vim.api.nvim_get_chan_info(chan_id)

	if not chan_info.id or not chan_info.client or chan_info.client.name ~= chan_name then
		vim.g.unity_rpc_channel = nil
		return nil
	end

	return chan_id
end

local unity_group = vim.api.nvim_create_augroup("UnityIntegration", { clear = true })
-- TODO: file create??
vim.api.nvim_create_autocmd("BufWritePost", {
	group = unity_group,
	callback = function(opts)
		local chan_id = vim.g.unity_rpc_channel

		if not chan_id then
			return
		end

		-- Check if the channel is still alive
		local chan_info = vim.api.nvim_get_chan_info(chan_id)

		if chan_info.id == nil then
			-- Unity process died or disconnected, cleanup
			vim.g.unity_rpc_channel = nil
			vim.api.nvim_clear_autocmds({ group = "UnityIntegrationGroup" })

			return
		end

		local filepath = opts.match
		vim.rpcnotify(chan_id, "UnityAssetSaved", filepath)
	end,
})

vim.api.nvim_create_user_command("UnitySync", function()
	local chan_id = check_channel()

	if not chan_id then
		return
	end

	vim.rpcnotify(chan_id, "UnitySyncAll")
end, { desc = "Manually synchronize project and reimport assets" })

local unity_group = vim.api.nvim_create_augroup("UnityAutoSyncGroup", { clear = true })

-- track newly created files when opened for the first time
vim.api.nvim_create_autocmd("BufNewFile", {
	group = unity_group,
	callback = function(opts)
		vim.b[opts.buf].is_new_file = true
	end,
})

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
		local is_new = vim.b[opts.buf].is_new_file

		if is_new then
			if vim.g.unity_auto_sync_on_create then
				vim.rpcnotify(chan_id, "UnityAssetCreated", filepath)
				vim.b[opts.buf].is_new_file = false
			end
		else
			if vim.g.unity_auto_sync_on_modify then
				vim.rpcnotify(chan_id, "UnityAssetSaved", filepath)
			end
		end
	end,
})
