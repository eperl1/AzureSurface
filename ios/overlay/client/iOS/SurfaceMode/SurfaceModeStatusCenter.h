#import <Foundation/Foundation.h>

typedef NS_ENUM(NSInteger, SurfaceModeStatusKind)
{
    SurfaceModeStatusKindIdle = 0,
    SurfaceModeStatusKindConnected,
    SurfaceModeStatusKindTabletSent,
    SurfaceModeStatusKindLaptopSent,
    SurfaceModeStatusKindError
};

extern NSString *const SurfaceModeStatusDidChangeNotification;

@interface SurfaceModeStatusCenter : NSObject

+ (instancetype)sharedCenter;

@property(readonly, nonatomic) NSString *statusText;
@property(readonly, nonatomic) NSString *detailText;
@property(readonly, nonatomic) SurfaceModeStatusKind kind;

- (void)updateKind:(SurfaceModeStatusKind)kind title:(NSString *)title detail:(NSString *)detail;

@end
