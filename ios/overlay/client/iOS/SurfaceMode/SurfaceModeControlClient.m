#import "SurfaceModeControlClient.h"

#import <GameController/GameController.h>
#import <dispatch/dispatch.h>

#import "SurfaceModeSettings.h"

NSString *const SurfaceModeControlClientDidChangeNotification = @"SurfaceModeControlClientDidChangeNotification";

@implementation SurfaceModeControlClient
{
    SurfaceModeControlTarget _lastSentTarget;
    BOOL _hasLastSentTarget;
    NSURLSession *_session;
}

+ (instancetype)sharedClient
{
    static SurfaceModeControlClient *shared = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        shared = [[self alloc] init];
    });
    return shared;
}

- (instancetype)init
{
    if ((self = [super init]))
    {
        NSURLSessionConfiguration *configuration = [NSURLSessionConfiguration ephemeralSessionConfiguration];
        configuration.timeoutIntervalForRequest = 4.0;
        configuration.timeoutIntervalForResource = 6.0;
        _session = [[NSURLSession sessionWithConfiguration:configuration] retain];
    }
    return self;
}

- (void)dealloc
{
    [_session invalidateAndCancel];
    [_session release];
    [super dealloc];
}

- (void)syncHardwareKeyboardStateWithReason:(NSString *)reason force:(BOOL)force
{
    BOOL keyboardConnected = (GCKeyboard.coalescedKeyboard != nil);
    SurfaceModeControlTarget target = keyboardConnected ? SurfaceModeControlTargetLaptop : SurfaceModeControlTargetTablet;
    if (!force && _hasLastSentTarget && _lastSentTarget == target)
    {
        return;
    }

    [self sendTarget:target reason:reason];
}

- (void)testConnection
{
    [self sendTarget:SurfaceModeControlTargetPing reason:@"test"];
}

- (void)sendTarget:(SurfaceModeControlTarget)target reason:(NSString *)reason
{
    NSString *host = [SurfaceModeSettings host];
    NSInteger port = [SurfaceModeSettings port];
    NSString *token = [SurfaceModeSettings token];
    if (host.length == 0 || port <= 0 || token.length == 0)
    {
        [[SurfaceModeStatusCenter sharedCenter] updateKind:SurfaceModeStatusKindError
                                                     title:@"Connection error"
                                                    detail:@"Set the host, port, and token first."];
        return;
    }

    NSString *command = [self commandForTarget:target];
    NSDateFormatter *formatter = [[[NSDateFormatter alloc] init] autorelease];
    formatter.locale = [NSLocale localeWithLocaleIdentifier:@"en_US_POSIX"];
    formatter.timeZone = [NSTimeZone timeZoneWithAbbreviation:@"UTC"];
    formatter.dateFormat = @"yyyy-MM-dd'T'HH:mm:ss'Z'";

    NSDictionary *body = @{
        @"command" : command,
        @"timestampUtc" : [formatter stringFromDate:[NSDate date]],
        @"nonce" : [[NSUUID UUID] UUIDString],
        @"source" : [SurfaceModeSettings sourceLabel],
        @"reason" : reason ?: @""
    };

    NSURL *url = [NSURL URLWithString:[NSString stringWithFormat:@"http://%@:%ld/api/mode", host, (long)port]];
    NSMutableURLRequest *request = [NSMutableURLRequest requestWithURL:url];
    request.HTTPMethod = @"POST";
    [request setValue:@"application/json" forHTTPHeaderField:@"Content-Type"];
    [request setValue:[NSString stringWithFormat:@"Bearer %@", token] forHTTPHeaderField:@"Authorization"];
    request.HTTPBody = [NSJSONSerialization dataWithJSONObject:body options:0 error:nil];

    NSURLSessionDataTask *task = [_session dataTaskWithRequest:request
                                            completionHandler:^(NSData *data, NSURLResponse *response, NSError *error) {
        if (error)
        {
            [[SurfaceModeStatusCenter sharedCenter] updateKind:SurfaceModeStatusKindError
                                                         title:@"Connection error"
                                                        detail:error.localizedDescription];
            NSLog(@"[SurfaceMode] %@ failed: %@", command, error.localizedDescription);
            return;
        }

        NSInteger statusCode = [(NSHTTPURLResponse *)response statusCode];
        if (statusCode == 200)
        {
            self->_lastSentTarget = target;
            self->_hasLastSentTarget = YES;
            [[SurfaceModeStatusCenter sharedCenter] updateKind:(target == SurfaceModeControlTargetPing
                                                                    ? SurfaceModeStatusKindConnected
                                                                    : (target == SurfaceModeControlTargetLaptop
                                                                           ? SurfaceModeStatusKindLaptopSent
                                                                           : SurfaceModeStatusKindTabletSent))
                                                         title:(target == SurfaceModeControlTargetPing
                                                                    ? @"Connected"
                                                                    : (target == SurfaceModeControlTargetLaptop
                                                                           ? @"Laptop event sent"
                                                                           : @"Tablet event sent"))
                                                        detail:(target == SurfaceModeControlTargetPing
                                                                    ? @"The control channel responded successfully."
                                                                    : @"Windows accepted the mode change.")];
            NSLog(@"[SurfaceMode] %@ succeeded", command);
            return;
        }

        NSString *message = [NSString stringWithFormat:@"HTTP %ld", (long)statusCode];
        [[SurfaceModeStatusCenter sharedCenter] updateKind:SurfaceModeStatusKindError
                                                     title:@"Connection error"
                                                    detail:message];
        NSLog(@"[SurfaceMode] %@ failed: %@", command, message);
    }];
    [task resume];
}

- (NSString *)commandForTarget:(SurfaceModeControlTarget)target
{
    switch (target)
    {
        case SurfaceModeControlTargetLaptop:
            return @"LAPTOP";
        case SurfaceModeControlTargetPing:
            return @"PING";
        case SurfaceModeControlTargetTablet:
        default:
            return @"TABLET";
    }
}

@end
