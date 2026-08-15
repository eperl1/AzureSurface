#import <Foundation/Foundation.h>

@interface SurfaceModeSettings : NSObject

+ (NSString *)host;
+ (void)setHost:(NSString *)host;

+ (NSInteger)port;
+ (void)setPort:(NSInteger)port;

+ (NSString *)sourceLabel;
+ (void)setSourceLabel:(NSString *)label;

+ (NSString *)token;
+ (void)setToken:(NSString *)token;

+ (BOOL)hasConnectionConfiguration;

@end
