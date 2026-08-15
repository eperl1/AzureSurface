#import <Foundation/Foundation.h>

#import "SurfaceModeStatusCenter.h"

typedef NS_ENUM(NSInteger, SurfaceModeControlTarget)
{
    SurfaceModeControlTargetTablet = 0,
    SurfaceModeControlTargetLaptop = 1,
    SurfaceModeControlTargetPing = 2
};

extern NSString *const SurfaceModeControlClientDidChangeNotification;

@interface SurfaceModeControlClient : NSObject

+ (instancetype)sharedClient;

- (void)syncHardwareKeyboardStateWithReason:(NSString *)reason force:(BOOL)force;
- (void)testConnection;

@end
