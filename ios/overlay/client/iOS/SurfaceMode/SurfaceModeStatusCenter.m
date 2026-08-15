#import "SurfaceModeStatusCenter.h"

#import <dispatch/dispatch.h>

NSString *const SurfaceModeStatusDidChangeNotification = @"SurfaceModeStatusDidChangeNotification";

@implementation SurfaceModeStatusCenter
{
    NSString *_statusText;
    NSString *_detailText;
    SurfaceModeStatusKind _kind;
}

+ (instancetype)sharedCenter
{
    static SurfaceModeStatusCenter *shared = nil;
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
        _statusText = [@"Not connected" copy];
        _detailText = [@"Waiting for the first keyboard event." copy];
        _kind = SurfaceModeStatusKindIdle;
    }
    return self;
}

- (void)dealloc
{
    [_statusText release];
    [_detailText release];
    [super dealloc];
}

- (NSString *)statusText
{
    return _statusText;
}

- (NSString *)detailText
{
    return _detailText;
}

- (SurfaceModeStatusKind)kind
{
    return _kind;
}

- (void)updateKind:(SurfaceModeStatusKind)kind title:(NSString *)title detail:(NSString *)detail
{
    @synchronized (self)
    {
        if (!title)
        {
            title = @"";
        }

        if (!detail)
        {
            detail = @"";
        }

        BOOL changed = (_kind != kind || ![_statusText isEqualToString:title] || ![_detailText isEqualToString:detail]);
        if (!changed)
        {
            return;
        }

        [_statusText release];
        [_detailText release];
        _statusText = [title copy];
        _detailText = [detail copy];
        _kind = kind;
    }

    [[NSNotificationCenter defaultCenter] postNotificationName:SurfaceModeStatusDidChangeNotification object:self];
}

@end
