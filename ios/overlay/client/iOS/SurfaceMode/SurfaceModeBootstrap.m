#import "SurfaceModeBootstrap.h"

#import <GameController/GameController.h>
#import <UIKit/UIKit.h>

#import "SurfaceModeControlClient.h"
#import "SurfaceModeSettings.h"

@implementation SurfaceModeBootstrap

+ (void)load
{
    @autoreleasepool
    {
        [[NSUserDefaults standardUserDefaults] registerDefaults:@{
            @"surface_mode.host" : @"",
            @"surface_mode.port" : @(47889),
            @"surface_mode.source" : @"FreeRDP iPad"
        }];

        NSNotificationCenter *center = [NSNotificationCenter defaultCenter];
        [center addObserver:self selector:@selector(handleKeyboardNotification:)
                       name:GCKeyboardDidConnectNotification object:nil];
        [center addObserver:self selector:@selector(handleKeyboardNotification:)
                       name:GCKeyboardDidDisconnectNotification object:nil];
        [center addObserver:self selector:@selector(handleAppActive:)
                       name:UIApplicationDidBecomeActiveNotification object:nil];
        [center addObserver:self selector:@selector(handleLaunch:)
                       name:UIApplicationDidFinishLaunchingNotification object:nil];
    }
}

+ (void)handleLaunch:(NSNotification *)notification
{
    (void)notification;
    [[SurfaceModeControlClient sharedClient] syncHardwareKeyboardStateWithReason:@"launch" force:YES];
}

+ (void)handleAppActive:(NSNotification *)notification
{
    (void)notification;
    [[SurfaceModeControlClient sharedClient] syncHardwareKeyboardStateWithReason:@"active" force:YES];
}

+ (void)handleKeyboardNotification:(NSNotification *)notification
{
    (void)notification;
    [[SurfaceModeControlClient sharedClient] syncHardwareKeyboardStateWithReason:@"keyboard" force:NO];
}

@end
