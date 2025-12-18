using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace DalamudACT;

public class DalamudApi
{
    public static void Initialize(IDalamudPluginInterface pluginInterface)
        => pluginInterface.Create<DalamudApi>();

    // @formatter:off
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ICommandManager Commands { get; private set; } = null!;
    [PluginService] public static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] public static IDataManager GameData { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static ICondition Conditions { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    
    [PluginService] public static ITargetManager Targets { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider Interop { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static IPartyList PartyList { get; private set; } = null!;
    // @formatter:on


    [PluginService] public static IChatGui ChatGui { get; private set; }
    [PluginService] public static IKeyState KeyState { get; private set; }
    [PluginService] public static IToastGui Toasts { get; private set; }
    [PluginService] public static IGameConfig GameConfig { get; private set; }
    [PluginService] public static ITextureProvider TextureProvider { get; private set; }
    [PluginService] public static ITextureSubstitutionProvider TextureSubstitutionProvider { get; private set; }
    [PluginService] public static ITextureReadbackProvider TextureReadbackProvider { get; private set; }
    [PluginService] public static IAddonLifecycle AddonLifecycle { get; private set; }
    [PluginService] public static IDtrBar DtrBar { get; private set; }
    [PluginService] public static IDutyState DutyState { get; private set; }
    [PluginService] public static INotificationManager NotificationManager { get; private set; }
    [PluginService] public static IContextMenu ContextMenu { get; private set; }
    [PluginService] public static INamePlateGui NamePlateGui { get; private set; }
    [PluginService] public static IPlayerState PlayerState { get; private set; }
    

    public static async Task<T> RunOnFrameworkThread<T>(Func<T> func, [CallerMemberName] string callerMember = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
    {
        var fileName = Path.GetFileNameWithoutExtension(callerFilePath);
        if (!Framework.IsInFrameworkUpdateThread)
        {
            var result = await Framework.RunOnFrameworkThread(func).ContinueWith((task) => task.Result).ConfigureAwait(false);
            while (Framework.IsInFrameworkUpdateThread) // yield the thread again, should technically never be triggered
            {
                await Task.Delay(1).ConfigureAwait(false);
            }
            return result;
        }

        return func.Invoke();
    }
}