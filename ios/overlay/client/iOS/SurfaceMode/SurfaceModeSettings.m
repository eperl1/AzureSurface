#import "SurfaceModeSettings.h"

#import "SFHFKeychainUtils.h"

static NSString *const kSurfaceModeHostKey = @"surface_mode.host";
static NSString *const kSurfaceModePortKey = @"surface_mode.port";
static NSString *const kSurfaceModeSourceKey = @"surface_mode.source";
static NSString *const kSurfaceModeKeychainUsername = @"surface_mode_token";
static NSString *const kSurfaceModeKeychainServer = @"com.freerdp.surfacemode";
static NSString *const kSurfaceModeDefaultSource = @"FreeRDP iPad";

@implementation SurfaceModeSettings

+ (NSUserDefaults *)defaults
{
    return [NSUserDefaults standardUserDefaults];
}

+ (NSString *)host
{
    NSString *host = [[self defaults] stringForKey:kSurfaceModeHostKey];
    return host ? host : @"";
}

+ (void)setHost:(NSString *)host
{
    if (!host)
    {
        host = @"";
    }
    host = [host stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];
    [[self defaults] setObject:host forKey:kSurfaceModeHostKey];
}

+ (NSInteger)port
{
    NSNumber *port = [[self defaults] objectForKey:kSurfaceModePortKey];
    if (port)
    {
        return [port integerValue];
    }
    return 47889;
}

+ (void)setPort:(NSInteger)port
{
    if (port <= 0)
    {
        port = 47889;
    }
    [[self defaults] setInteger:port forKey:kSurfaceModePortKey];
}

+ (NSString *)sourceLabel
{
    NSString *label = [[self defaults] stringForKey:kSurfaceModeSourceKey];
    return label.length > 0 ? label : kSurfaceModeDefaultSource;
}

+ (void)setSourceLabel:(NSString *)label
{
    if (!label || !label.length)
    {
        label = kSurfaceModeDefaultSource;
    }
    label = [label stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];
    [[self defaults] setObject:label forKey:kSurfaceModeSourceKey];
}

+ (NSString *)token
{
    NSError *error = nil;
    NSString *token = [SFHFKeychainUtils getPasswordForUsername:kSurfaceModeKeychainUsername
                                                  andServerName:kSurfaceModeKeychainServer
                                                          error:&error];
    if (error || !token)
    {
        return @"";
    }
    return token;
}

+ (void)setToken:(NSString *)token
{
    if (!token)
    {
        token = @"";
    }
    token = [token stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];

    NSError *error = nil;
    BOOL success = [SFHFKeychainUtils storeUsername:kSurfaceModeKeychainUsername
                                         andPassword:token
                                       forServerName:kSurfaceModeKeychainServer
                                      updateExisting:YES
                                               error:&error];
    if (!success)
    {
        NSLog(@"[SurfaceMode] failed to store token in Keychain: %@", error);
    }
}

+ (BOOL)hasConnectionConfiguration
{
    return [self host].length > 0 && [self port] > 0 && [self token].length > 0;
}

@end
